namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public sealed record IntakeResult(Guid? JobId, short? RunNo, IntakeOutcome Outcome, string? RunStatus)
{
    public string RunReason { get; init; } = "New";
    public string? ChangesJson { get; init; }
    public string? RereadSkipped { get; init; }
    public long? RequiredPages { get; init; }
    public long? AvailablePages { get; init; }
    private IntakeResult() : this(null, null, default, null) { }
}
