using NUnit.Framework;
using Assert = ISTDocAi.UI.Server.Tests.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Infrastructure;

using ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
using ISTDocAi.Infrastructure.Tests.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace ISTDocAi.UI.Server.Tests.DocAi;
public sealed class DatabaseReadinessTests
{
    [TestCase(null, "runtime")]
    [TestCase("  ", "runtime")]
    [TestCase("migration", "migration")]
    public void Options_bind_defaults_and_connection_fallback(string? configured, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "runtime", ["DocAi:Database:MigrationConnectionString"] = configured
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddDocAiDatabase(configuration, false);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        Assert.True(options.AllowDatabaseCreation);
        Assert.True(options.MigrateOnStartup);
        Assert.Equal(900, options.InitializationTimeoutSeconds);
        Assert.Equal(300, options.CommandTimeoutSeconds);
        Assert.Equal(expected, options.MigrationConnectionString);
    }

    [Test]
    public async Task Invalid_configuration_fails_once_without_faulting_the_hosted_service()
    {
        var gate = new DatabaseReadiness();
        var logger = new FailureLogger();
        using var service = new DeploymentInitializationHost(new DatabaseOptions(), gate, logger);
        await service.StartAsync(default);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DatabaseReadinessState.Failed, gate.Status.State);
        Assert.Equal("DATABASE_CONFIGURATION_INVALID", gate.Status.Code);
        Assert.Single(logger.Lines);
        Assert.Contains("database.initialization_failed", logger.Lines[0]);
        Assert.DoesNotContain("Exception", logger.Lines[0]);
    }

    private sealed class FailureLogger : Microsoft.Extensions.Logging.ILogger<UnifiedDatabaseInitializer>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception);
            Lines.Add(formatter(state, exception));
        }
    }

}
