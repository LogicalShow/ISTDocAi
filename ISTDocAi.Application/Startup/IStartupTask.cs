namespace ISTDocAi.Application.Startup;

public interface IStartupTask
{
    StartupStage Stage { get; }
    int Order { get; }
    Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
