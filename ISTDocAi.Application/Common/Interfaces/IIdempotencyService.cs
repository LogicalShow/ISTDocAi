namespace ISTDocAi.Application.Common.Interfaces;

public interface IIdempotencyService
{
    Task<string?> GetAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string key,
        string value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);
}
