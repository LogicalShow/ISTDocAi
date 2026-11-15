namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IJobRepository
{
    Task<CancelJobResult?> CancelAsync(short consumerId, string requestId, CancellationToken cancellationToken);
    Task<WriteOutcome> FinishCancellationAsync(Lease lease, CancellationToken cancellationToken);
    Task<int> DailyPagesAsync(short consumerId, CancellationToken cancellationToken);
    Task<IntakeResult> IntakeAsync(IntakeRequest request, CancellationToken cancellationToken);
    Task<RunOutcome?> GetRunAsync(Guid jobId, short consumerId, short? runNo, CancellationToken cancellationToken);
    Task<RunPosition?> GetPositionAsync(Guid jobId, CancellationToken cancellationToken);
    Task<WriteOutcome> CompleteAsync(Completion request, CancellationToken cancellationToken);
    Task<WriteOutcome> FailAsync(Failure request, CancellationToken cancellationToken);
    Task<WriteOutcome> ClassifierSucceededAsync(ClassifierSuccess request, CancellationToken cancellationToken);
    Task<ClassMapping?> GetClassMappingAsync(string classifierModelId, string className, CancellationToken cancellationToken);
}
