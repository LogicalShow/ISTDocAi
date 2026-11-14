namespace ISTDocAi.Application.Common.Interfaces.DocAi;

public enum DatabaseReadinessState { Pending, Ready, Failed }
public sealed record DatabaseReadinessStatus(DatabaseReadinessState State, string Code);
public interface IDatabaseReadiness
{
    DatabaseReadinessStatus Status { get; }
    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
}

