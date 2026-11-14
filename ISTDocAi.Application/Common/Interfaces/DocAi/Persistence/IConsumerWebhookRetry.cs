namespace ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;

public interface IConsumerWebhookRetry
{
    Task<int> MakeDueAsync(short consumerId, CancellationToken cancellationToken);
}
