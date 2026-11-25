using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using System.Text.Json;
namespace ISTDocAi.Application.Services.DocAi.Intake;
public sealed class SubmitJob(IJobRepository jobs, IDocumentStore documents, IFileInspector inspector, ICatalogReader catalog, TimeProvider clock)
{
    public async Task<Submission> ExecuteAsync(Consumer consumer, PreparedSubmission file, CancellationToken ct)
    {
        if (file.DocumentType == "AUTO" && file.ShippingLine is not null)
            throw new IntakeException(422, "INVALID_REQUEST", "Invalid AUTO route", "AUTO cannot specify a shipping line.");
        var info = inspector.Inspect(file.Path, ct);
        if (info.PageCount > consumer.MaxPagesPerJob)
            throw new IntakeException(422, "TOO_MANY_PAGES", "The document has more pages than allowed", $"{info.PageCount} pages; the limit for this consumer is {consumer.MaxPagesPerJob}.");
        if (info.PageCount < 1) throw IntakeException.Invalid();
        var models = await catalog.ReadAsync(ct);
        if (!models.Any(m => m.DocumentTypeCode == file.DocumentType && (file.ShippingLine is null
            ? m.ShippingLineCode is null && (m.Kind == "classify" || file.DocumentType != "AUTO" && m.Kind == "extract")
            : m.Kind == "extract" && m.ShippingLineCode == file.ShippingLine)))
            throw new IntakeException(422, "NOT_IN_CATALOG", "Document is not in the catalog", "Choose a document type and shipping line from the catalog.");
        var request = new IntakeRequest(Guid.NewGuid(), consumer.ConsumerId, file.ClientRequestId, file.DocumentType,
            file.ShippingLine, file.Hash, file.FileName, info.ContentType, file.Size, checked((short)info.PageCount), null, file.CorrelationId, file.Reprocess);
        var result = await jobs.IntakeAsync(request, ct);
        if (result.Outcome == IntakeOutcome.NeedsUpload)
        {
            var now = clock.GetUtcNow();
            // Advisory upload saving check. S2 rechecks atomically before creating any run.
            if ((long)await jobs.DailyPagesAsync(consumer.ConsumerId, ct) + info.PageCount > consumer.DailyPageCap)
                throw new IntakeException(429, "DAILY_CAP_REACHED", "Daily page cap reached", "Wait until the next UTC day.", (int)Math.Ceiling((now.UtcDateTime.Date.AddDays(1) - now.UtcDateTime).TotalSeconds));
            var path = $"documents/{consumer.Code}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{info.Extension}";
            await using var stream = File.OpenRead(file.Path);
            await documents.PutAsync(path, stream, info.ContentType, ct);
            result = await jobs.IntakeAsync(request with { BlobPath = path }, ct);
        }
        if (result.JobId is null || result.RunNo is null || result.RunStatus is null) throw new InvalidOperationException("Invalid intake outcome.");
        return new(new SubmitJobResponse { JobId = result.JobId.Value, RunNo = result.RunNo.Value,
            RereadSkipped = result.RereadSkipped, RunReason = result.RunReason, Changes = RunChangeJson.Read(result.ChangesJson), Status = result.RunStatus, StatusUrl = $"/api/v2/jobs/{result.JobId}?runNo={result.RunNo}", PageCount = info.PageCount },
            result.Outcome is IntakeOutcome.Created or IntakeOutcome.Requeued);
    }
}
