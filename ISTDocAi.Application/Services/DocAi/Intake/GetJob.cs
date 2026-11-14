using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using System.Text.Json;
namespace ISTDocAi.Application.Services.DocAi.Intake;
public sealed class GetJob(IJobRepository jobs)
{
    public async Task<JobResponse> ExecuteAsync(Guid id, Consumer consumer, short? runNo, CancellationToken ct)
    {
        var run = await jobs.GetRunAsync(id, consumer.ConsumerId, runNo, ct)
            ?? throw new IntakeException(404, "JOB_NOT_FOUND", "Job not found", "No such job or run exists for this consumer.");
        CanonicalResultCodec.ValidateType(run.ResultType);
        var result = run.ResultJson is null ? null : CanonicalResultCodec.Deserialize(run.ResultJson, run.ResultType);
        if (result is not null && run.ResolvedDocumentTypeCode is not null && run.ResolvedDocumentTypeCode != result.DocumentTypeCode)
            throw new JsonException("Stored result and run document types disagree.");
        return new() { JobId = run.JobId, RunNo = run.RunNo, ClientRequestId = run.ClientRequestId ?? "", Status = run.Status, RunReason = run.RunReason, Changes = RunChangeJson.Read(run.ChangesJson),
            Error = run.Status != "Failed" || run.ErrorCode is null ? null : new(run.ErrorCode, run.ErrorMessage ?? ""),
            Result = result, RequestedDocumentType = run.RequestedDocumentTypeCode,
            DocumentType = run.ResolvedDocumentTypeCode ?? result?.DocumentTypeCode,
            ResultType = run.ResultType ?? (result is null ? null : CanonicalResultCodec.TypeOf(result)) };
    }
}


