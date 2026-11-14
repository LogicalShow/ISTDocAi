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
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ISTDocAi.UI.Server.Tests.DocAi;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
internal sealed class MappingReadinessSqlTests : GuardedDeploymentTests
{
    private readonly SqlDatabase db = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL"))
        ? Task.CompletedTask : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();

    [Test, Category("Sql")]
    public async Task Active_unbound_profile_returns_503_then_200_after_binding_and_sixty_second_cache()
    {
        await db.Execute("UPDATE dbo.ModelRegistry SET Status='Retired' WHERE ModelId='inv-line'");
        using var factory = new Factory(db.ConnectionString);
        using var client = factory.Client(null); // TestServer only; Azure and webhook dependencies are fakes.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        await db.Execute("""
            UPDATE dbo.ModelRegistry SET Status='Draft',ActivatedAtUtc=NULL WHERE ModelId='bl-v1';
            INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,
                ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference)
            SELECT 'ready-stage4',1,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,
                SealNumberField,BillsOfLadingField,'Stage 4 readiness test' FROM dbo.MappingProfiles WHERE ProfileCode='legacy';
            DECLARE @profile bigint=SCOPE_IDENTITY();
            INSERT dbo.MappingFields SELECT @profile,MapKind,CanonicalKey,AzureName FROM dbo.MappingFields
                WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='legacy');
            INSERT dbo.ModelMappingBindings VALUES('bl-v1',@profile);
            UPDATE dbo.ModelRegistry SET Status='Active',ActivatedAtUtc=SYSUTCDATETIME() WHERE ModelId='bl-v1';
            """);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        factory.Clock.Now += TimeSpan.FromSeconds(60);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
    [Test, Category("Sql")]
    public async Task Fresh_server_model_SQL_makes_Sql_readiness_200()
    {
        await using var fresh = new DisposableDatabase();
        var gate = new DatabaseReadiness();
        using var initializer = new DeploymentInitializationHost(new DatabaseOptions { MigrationConnectionString = fresh.ConnectionString }, gate);
        await initializer.StartAsync(default);
        await initializer.ExecuteTask!.WaitAsync(TimeSpan.FromMinutes(16));
        Assert.Equal(DatabaseReadinessState.Ready, gate.Status.State);
        Assert.Equal("0", await fresh.ScalarTarget("SELECT COUNT(*) FROM dbo.ModelRegistry", default));
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","Sql","mapping-readiness-models.sql"));
        await fresh.ExecuteBatches(System.Text.RegularExpressions.Regex.Split(sql, @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase).Where(b => !string.IsNullOrWhiteSpace(b)), default);
        using var factory = new Factory(fresh.ConnectionString);
        using var client = factory.Client(null);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
    [Category("Sql")]
    [TestCase("DocAiLocal", true)]
    [TestCase("DocAiIis", false)]
    public async Task Migration_bound_existing_installations_keep_profiles_and_registration_is_noop(string installation, bool includeRetired)
    {
        await using var existing = new DisposableDatabase();
        await using (var baseline = existing.Context())
            await Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(baseline)
                .MigrateAsync("20261101000000_InitialSchema");
        await existing.Execute("""
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status,TestFieldAccuracy,MinFieldConfidence,ActivatedAtUtc) VALUES
            ('bl-v3','BL','extract',3,'Active',0.97,0.8,SYSUTCDATETIME()),
            ('manifest-v3','MANIFEST','extract',3,'Active',0.92,0.8,SYSUTCDATETIME()),
            ('IST-Classifier-v6','AUTO','classify',6,'Active',0.91,0.5,SYSUTCDATETIME());
            INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode,ShippingLineCode) VALUES
            ('IST-Classifier-v6','bill-of-lading','BL',NULL),('IST-Classifier-v6','cargo-manifest','MANIFEST',NULL),('IST-Classifier-v6','other',NULL,NULL);
            """, default);
        if (includeRetired) await existing.Execute("""
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES
            ('bl-v2','BL','extract',2,'Retired'),('manifest-v2','MANIFEST','extract',2,'Retired');
            """, default);
        await using (var latest = existing.Context())
            await Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(latest).MigrateAsync();
        const string snapshot = "SELECT (SELECT * FROM dbo.ModelRegistry ORDER BY ModelId FOR JSON PATH) m,(SELECT * FROM dbo.MappingProfiles ORDER BY ProfileId FOR JSON PATH) p,(SELECT * FROM dbo.MappingFields ORDER BY ProfileId,MapKind,CanonicalKey FOR JSON PATH) f,(SELECT * FROM dbo.ModelMappingBindings ORDER BY ModelId FOR JSON PATH) b,(SELECT * FROM dbo.ClassifierClasses ORDER BY ClassifierModelId,ClassName FOR JSON PATH) c FOR JSON PATH";
        var before = await existing.ScalarTarget(snapshot, default);
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","Sql","mapping-readiness-models.sql"));
        for (var run=0; run<2; run++)
            await existing.ExecuteBatches(System.Text.RegularExpressions.Regex.Split(sql, @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase).Where(b => !string.IsNullOrWhiteSpace(b)), default);
        Assert.True(before == await existing.ScalarTarget(snapshot, default), installation + " changed during registration rerun");
        using var factory = new Factory(existing.ConnectionString);
        using var client = factory.Client(null);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
    private sealed class Factory(string connectionString) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["DocAi:Mapping:Source"] = "Sql", ["ConnectionStrings:DefaultConnection"] = connectionString }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISqlConnectionFactory>();
                services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(Options.Create(
                    new DocAiSqlOptions { ConnectionString = connectionString })));
                services.RemoveAll<IDocumentAnalyzer>();
                services.AddSingleton<IDocumentAnalyzer>(new ISTDocAi.Infrastructure.Tests.DocAi.Worker.WorkerFakes());
                services.RemoveAll<ICallbackDirectory>();
                services.AddSingleton<ICallbackDirectory>(new NoCallbacks());
            });
        }
    }
    private sealed class NoCallbacks : ICallbackDirectory
    {
        public Task<IReadOnlyList<CallbackConsumer>> ReadAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CallbackConsumer>>([]);
    }
}
