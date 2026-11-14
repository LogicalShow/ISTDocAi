namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface ICallbackDirectory { Task<IReadOnlyList<CallbackConsumer>> ReadAsync(CancellationToken ct); }
