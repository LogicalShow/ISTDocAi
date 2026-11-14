namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IConsumerSource { Task<IReadOnlyList<Consumer>> GetConsumersAsync(CancellationToken ct); }
