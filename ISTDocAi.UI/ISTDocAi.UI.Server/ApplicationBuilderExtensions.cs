using ISTDocAi.UI.Server.Startup;

namespace ISTDocAi.UI.Server;

public static class ApplicationBuilderExtensions
{
    public static async Task RunStartupPipelineAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        
        var pipeline = services.GetRequiredService<StartupPipeline>();
        await pipeline.ExecuteAsync(cancellationToken);
    }
}
