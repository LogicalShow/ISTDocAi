namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface ISignalReader
{
    Task<int?> OldestDueSecondsAsync(CancellationToken ct);
    Task<IReadOnlyList<UnpricedUsage>> UnpricedUsageAsync(CancellationToken ct);
}

// Shared by producers and the infrastructure alert contract tests.
