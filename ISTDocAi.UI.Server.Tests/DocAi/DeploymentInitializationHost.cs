using ISTDocAi.Application;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Startup;
using ISTDocAi.Infrastructure;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Startup.DataInitialization;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ISTDocAi.Infrastructure.Data;
namespace ISTDocAi.UI.Server.Tests.DocAi;

// Owns a real unified initializer and all of its scoped seed dependencies.
internal sealed class DeploymentInitializationHost : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly DatabaseInitializationService service;
    public DeploymentInitializationHost(DatabaseOptions options, DatabaseReadiness gate, ILogger<UnifiedDatabaseInitializer>? logger=null,
        RestrictedPackageConnections? restriction=null)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"]=options.MigrationConnectionString,
            ["TemplateEndpoints:Enabled"]="false" }).Build();
        var services=new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IConfiguration>(config);
        services.AddApplication(); services.AddInfrastructure(options.MigrationConnectionString ?? "Server=invalid.invalid;Database=NeverConnect;Integrated Security=True",false);
        if(restriction is not null) services.AddDbContext<AppDbContext>((_,settings)=>settings.AddInterceptors(restriction));
        services.AddScoped<IStartupTask,SeederExecutorTask>();
        services.AddSingleton(gate); services.AddSingleton<IDatabaseReadiness>(gate);
        services.AddSingleton<IOptions<DatabaseOptions>>(Options.Create(options));
        services.AddSingleton<UnifiedDatabaseInitializer>();
        if(logger is not null) services.AddSingleton(logger);
        provider=services.BuildServiceProvider();
        service=new(provider.GetRequiredService<UnifiedDatabaseInitializer>());
    }
    public Task StartAsync(CancellationToken token)=>service.StartAsync(token);
    public Task? ExecuteTask=>service.ExecuteTask;
    public void Dispose() { service.Dispose(); provider.Dispose(); }
}
