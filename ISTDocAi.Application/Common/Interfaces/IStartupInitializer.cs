namespace ISTDocAi.Application.Common.Interfaces;

public interface IStartupInitializer
{
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
}
