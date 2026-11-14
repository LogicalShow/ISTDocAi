using ISTDocAi.Infrastructure;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace ISTDocAi.UI.Server.Tests;

public sealed class ConnectionConfigurationTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void Default_connection_is_required(string? value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = value }).Build();
        Assert.That(() => DatabaseConfiguration.RuntimeConnection(config), Throws.InvalidOperationException
            .With.Message.EqualTo("ConnectionStrings:DefaultConnection is required."));
    }

    [Test]
    public void Removed_key_fails_startup_without_exposing_its_value()
    {
        using var factory = new BackendFactory(false);
        using var stale = factory.WithWebHostBuilder(b => b.UseSetting("DocAi:Sql:ConnectionString", "secret-marker"));
        Assert.That(() => stale.CreateClient(), Throws.InvalidOperationException.With.Message.EqualTo(
            "DocAi:Sql:ConnectionString was removed; set ConnectionStrings:DefaultConnection."));
    }

    [Test]
    public void Sql_binding_uses_default_connection_and_preserves_timeouts()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "Server=invalid.invalid;Database=NeverConnect",
            ["DocAi:Sql:CommandTimeoutSeconds"] = "47",
            ["DocAi:Sql:RetentionTimeoutSeconds"] = "900" }).Build();
        var services = new ServiceCollection().AddDocAiSql(config);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DocAiSqlOptions>>().Value;
        Assert.That(options.ConnectionString, Is.EqualTo(config.GetConnectionString("DefaultConnection")));
        Assert.That(options.CommandTimeoutSeconds, Is.EqualTo(47));
        Assert.That(options.RetentionTimeoutSeconds, Is.EqualTo(900));
    }

    [TestCase("Development")]
    [TestCase("Production")]
    public void Host_uses_explicit_test_connection_and_production_does_not_load_development(string environment)
    {
        using var factory = new BackendFactory(false, environment);
        using var client = factory.CreateClient();
        var config = factory.Services.GetRequiredService<IConfiguration>();
        const string target = "Server=invalid.invalid;Database=BackendTestsNeverConnect;Integrated Security=True";
        Assert.That(config.GetConnectionString("DefaultConnection"), Is.EqualTo(target));
        Assert.That(factory.Services.GetRequiredService<IOptions<DocAiSqlOptions>>().Value.ConnectionString, Is.EqualTo(target));
        var files = ((IConfigurationRoot)config).Providers.OfType<Microsoft.Extensions.Configuration.Json.JsonConfigurationProvider>();
        Assert.That(files.Any(p => p.Source.Path == "appsettings.Development.json"), Is.EqualTo(environment == "Development"));
    }

    [Test]
    public void Test_can_explicitly_override_the_connection()
    {
        using var factory = new BackendFactory(false);
        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:DefaultConnection",
            "Server=invalid.invalid;Database=ExplicitTestTarget;Integrated Security=True"));
        using var client = configured.CreateClient();
        Assert.That(configured.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"),
            Does.Contain("Database=ExplicitTestTarget"));
    }
}
