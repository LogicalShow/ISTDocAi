namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IModelThresholdSource { Task<decimal?> GetThresholdAsync(string modelId, CancellationToken ct); }
