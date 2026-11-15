using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Data.Repositories.DocAi;

public sealed class SqlJobRepository : IJobRepository
{
    private readonly SqlExecution execution;
    private readonly ISqlConnectionFactory connections;
    private readonly IOptions<DocAiSqlOptions> options;
    private readonly IMappingProfileResolver profiles;
    internal SqlJobRepository(SqlExecution execution, ISqlConnectionFactory connections, IOptions<DocAiSqlOptions> options, IMappingProfileResolver profiles)
    {
        this.execution = execution;
        this.connections = connections; this.options = options; this.profiles = profiles;
    }

    public Task<CancelJobResult?> CancelAsync(short consumerId, string requestId, CancellationToken cancellationToken) =>
        execution.One<CancelJobResult>("S32", new { consumerId, requestId }, cancellationToken);
    public Task<WriteOutcome> FinishCancellationAsync(Lease lease, CancellationToken cancellationToken) =>
        execution.Write("S33", new { lease.JobId, lease.LeaseToken }, true, cancellationToken);

    public async Task<int> DailyPagesAsync(short consumerId, CancellationToken cancellationToken) =>
        await execution.One<int>("S1", new { consumerId }, cancellationToken);

    public async Task<IntakeResult> IntakeAsync(IntakeRequest request, CancellationToken cancellationToken)
    {
        for (var retries = 0; ; retries++)
        {
            try
            {
                if (profiles is ISqlMappingProfileResolver)
                {
                    try
                    {
                        // AUTO has no extract route yet: validate every possible active extract model.
                        await using var connection = await connections.OpenAsync(cancellationToken);
                        var models = (await connection.QueryAsync<IntakeProfileModel>(new CommandDefinition("""
                            SELECT ModelId, DocumentTypeCode FROM dbo.ModelRegistry
                            WHERE Kind='extract' AND Status='Active'
                              AND (@DocumentTypeCode='AUTO' OR (DocumentTypeCode=@DocumentTypeCode
                                AND (ShippingLineCode=@RequestedLine OR (ShippingLineCode IS NULL AND @RequestedLine IS NULL))));
                            """, request, commandTimeout: options.Value.CommandTimeoutSeconds,
                            cancellationToken: cancellationToken))).AsList();
                        if (models.Count == 0) throw MappingProfiles.ConfigurationError();
                        foreach (var model in models) _ = profiles.Resolve(model.ModelId, model.DocumentTypeCode);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        throw MappingUnavailable();
                    }
                }
                // Bypass the 60-second catalog cache. S2 rechecks the observed completed route under its locks.
                var active = await execution.One<IntakeModelSnapshot>("S25", request, cancellationToken);
                var current = request with
                {
                    ExpectedRunNo = active?.RunNo, ExpectedDocumentType = active?.ResolvedDocumentTypeCode,
                    ExpectedLine = active?.ShippingLineCode, ActiveClassifierModelId = active?.ClassifierModelId,
                    ActiveExtractModelId = active?.ExtractModelId,
                    ActiveMappingFingerprint = active?.ExtractModelId is not { } modelId ? null :
                        MappingFingerprint.ComputeEffective(
                            ResolveIntakeProfile(modelId, active.ResolvedDocumentTypeCode!), active.ResolvedDocumentTypeCode!)
                };
                var result = await execution.One<IntakeResult>("S2", current, cancellationToken)
                    ?? throw new InvalidOperationException("S2 did not return its intake outcome.");
                if (result.Outcome == IntakeOutcome.QuotaExceeded)
                    throw new IntakeException(402, "QUOTA_EXHAUSTED",
                        "The page allowance is exhausted or insufficient for this document. Contact IST to renew.",
                        "The page allowance is exhausted or insufficient for this document. Contact IST to renew.",
                        requiredPages: result.RequiredPages, availablePages: result.AvailablePages);
                if (result.Outcome == IntakeOutcome.DailyCapExceeded)
                {
                    var now = DateTime.UtcNow;
                    throw new IntakeException(429, "DAILY_CAP_REACHED", "Daily page cap reached", "Wait until the next UTC day.",
                        (int)Math.Ceiling((now.Date.AddDays(1) - now).TotalSeconds));
                }
                return result;
            }
            catch (SqlException e) when (e.Number is 1205 or 50015 && retries < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(25, 100) * (retries + 1)), cancellationToken);
            }
        }
    }

    public Task<RunOutcome?> GetRunAsync(Guid jobId, short consumerId, short? runNo, CancellationToken cancellationToken) =>
        execution.One<RunOutcome>("S13", new { jobId, consumerId, runNo }, cancellationToken);

    public Task<RunPosition?> GetPositionAsync(Guid jobId, CancellationToken cancellationToken) =>
        execution.One<RunPosition>("S19", new { jobId }, cancellationToken);

    public Task<WriteOutcome> CompleteAsync(Completion request, CancellationToken cancellationToken) => execution.Write("S8", request, false, cancellationToken);

    public Task<WriteOutcome> FailAsync(Failure request, CancellationToken cancellationToken) => execution.Write("S9", request, false, cancellationToken);

    public Task<WriteOutcome> ClassifierSucceededAsync(ClassifierSuccess request, CancellationToken cancellationToken) => execution.Write("S18", request, false, cancellationToken);

    public Task<ClassMapping?> GetClassMappingAsync(string classifierModelId, string className, CancellationToken cancellationToken) =>
        execution.One<ClassMapping>("S20", new { classifierModelId, className }, cancellationToken);
    private static IntakeException MappingUnavailable() => new(500, "INTERNAL_ERROR", "Internal error",
        "An unexpected error occurred. Quote the correlationId when reporting it.");

    private AnalyzeResultMappingOptions ResolveIntakeProfile(string modelId, string documentType)
    {
        try { return profiles.Resolve(modelId, documentType, allowLegacyDocumentType: true); }
        catch (Exception) when (profiles is ISqlMappingProfileResolver) { throw MappingUnavailable(); }
    }
    private sealed record IntakeProfileModel(string ModelId, string DocumentTypeCode);
    private sealed record IntakeModelSnapshot(short RunNo, string? ResolvedDocumentTypeCode, string? ShippingLineCode,
        string? ClassifierModelId, string? ExtractModelId);
}
