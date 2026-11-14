namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IModelThresholdReader { Task<decimal?> ReadThresholdAsync(string modelId, CancellationToken ct); }
