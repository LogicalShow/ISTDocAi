namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface ICallbackSource { Task<IReadOnlyList<CallbackConsumer>> GetCallbacksAsync(CancellationToken ct); }
