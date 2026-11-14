using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Text.RegularExpressions;
using Dapper;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

/// <summary>Each test owns a new database. Connection failures fail visibly; integration tests are never silently skipped.</summary>
internal sealed class SqlDatabase
{
    private readonly string database = "DocAiTest_" + Guid.NewGuid().ToString("N");
    private readonly string master;
    private bool created;
    public bool SkipModelSeed { get; init; }
    public bool UseLegacySchema { get; init; }
    // Only for explicit script/grant oracles; runtime fixtures use migrations.
    public bool UseSchemaScript { get; init; }
    public string ConnectionString { get; }
    public SqlDocAiStore Store { get; }
    public SqlDatabase()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")
            ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5");
        builder.InitialCatalog = "master";
        master = builder.ConnectionString;
        builder.InitialCatalog = database;
        ConnectionString = builder.ConnectionString;
        var options = Options.Create(new DocAiSqlOptions { ConnectionString = ConnectionString });
        Store = new SqlDocAiStore(new SqlConnectionFactory(options), new SqlStatementCatalog(), options);
    }
    public async Task InitializeAsync()
    {
        SqlTestGuard.Require();
        try { await InitializeCoreAsync(); } catch { await DisposeAsync(); throw; }
    }
    private async Task InitializeCoreAsync()
    {
        await using var connection = new SqlConnection(master);
        try { await connection.OpenAsync(); }
        catch (SqlException e) { throw new InvalidOperationException("SQL integration tests NOT RUN: SQL Server is unreachable. Set DOCAI_TEST_SQL or start (localdb)\\MSSQLLocalDB.", e); }
        // Only a locally generated identifier is interpolated; never use a supplied database name for create/drop.
        await connection.ExecuteAsync($"CREATE DATABASE [{database}]");
        created = true;
        await connection.ExecuteAsync($"ALTER DATABASE [{database}] SET READ_COMMITTED_SNAPSHOT ON");
        if (UseLegacySchema || UseSchemaScript)
        {
            var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, UseLegacySchema ? "Fixtures/Sql/plan-2.9-schema.sql" : "01_docai_schema.sql"));
            foreach (var batch in Regex.Split(schema, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await Execute(batch);
        }
        else
        {
            await using var context = new DocAiDbContext(new DbContextOptionsBuilder<DocAiDbContext>()
                .UseSqlServer(ConnectionString, sql => sql.CommandTimeout(300)).Options);
            await context.Database.MigrateAsync();
        }
        await Execute("""
            INSERT dbo.Consumers(Code,Name,ProductCode,TenantCode,ApiKeyHash1,CallbackUrl,WebhookSecretName)
            VALUES('TEST','Test','FWD','TEST',CONVERT(binary(32),1),'https://test.example/callback','test-secret');
            """);
        if (SkipModelSeed) return;
        await Execute("""
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES
                ('bl-v1','BL','extract',1,'Active'), ('inv-classifier','INV','classify',1,'Active');
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,ShippingLineCode,Kind,Version,Status)
            VALUES('inv-line','INV','LINE','extract',1,'Active');

            """);
        await Execute(UseLegacySchema
            ? "INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,ShippingLineCode) VALUES('inv-classifier','line-class','LINE');"
            : "INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,ShippingLineCode,TargetDocumentTypeCode) VALUES('inv-classifier','line-class','LINE','INV');");
    }
    public async Task DisposeAsync()
    {
        if (!created) return;
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        await connection.ExecuteAsync($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        created = false;
    }
    public async Task<int> Execute(string sql, object? args = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteAsync(sql, args);
    }
    public async Task<T> Scalar<T>(string sql, object? args = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<T>(sql, args) ?? throw new InvalidOperationException("Expected scalar.");
    }
    public async Task<string> Snapshot() => await Scalar<string>("""
        SELECT (SELECT * FROM dbo.Jobs ORDER BY JobSeq FOR JSON PATH, INCLUDE_NULL_VALUES) jobs,
        (SELECT * FROM dbo.JobRuns ORDER BY JobId,RunNo FOR JSON PATH, INCLUDE_NULL_VALUES) runs,
        (SELECT * FROM dbo.AzureOperations ORDER BY OperationId FOR JSON PATH, INCLUDE_NULL_VALUES) operations,
        (SELECT * FROM dbo.UsageLedger ORDER BY UsageId FOR JSON PATH, INCLUDE_NULL_VALUES) ledger,
        (SELECT * FROM dbo.JobResults ORDER BY JobId,RunNo FOR JSON PATH, INCLUDE_NULL_VALUES) results,
        (SELECT * FROM dbo.WebhookDeliveries ORDER BY DeliveryId FOR JSON PATH, INCLUDE_NULL_VALUES) events
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        """);
    public static IntakeRequest Request(string type = "BL") => new(Guid.NewGuid(), 1, Guid.NewGuid().ToString("N"), type,
        null, System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray()), "file.pdf", "application/pdf", 100,
        2, "documents/test.pdf", "test-correlation");
    public async Task<ClaimedJob> NewClaim(string type = "BL")
    {
        await Store.IntakeAsync(Request(type), default);
        return await SqlClaimTestHelper.ExpectedAsync(Store.ClaimAsync);
    }
    public async Task<long> Accepted(ClaimedJob job, string kind = "extract", string model = "bl-v1")
    {
        var begin = await Store.BeginAsync(new(job.JobId, job.LeaseToken), kind, model, default);
        Assert.Equal(WriteOutcome.Applied, begin.Outcome);
        var id = Assert.IsType<long>(begin.OperationId);
        Assert.True(await Store.AcceptedAsync(id, "https://azure.example/operations/1", default));
        return id;
    }
}
