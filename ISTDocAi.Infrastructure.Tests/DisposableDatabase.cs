using NUnit.Framework;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace ISTDocAi.Infrastructure.Tests.DocAi;

internal sealed class DisposableDatabase : IAsyncDisposable
{
    public string Name { get; } = "DocAiMigrationTest_" + Guid.NewGuid().ToString("N");
    private readonly string master;
    private readonly string target;
    public string ConnectionString => target;
    public Task<string> ScalarTarget(string sql, CancellationToken ct) => Scalar(target, sql, ct);
    public async Task<SchemaCatalog> Catalog(CancellationToken ct)
    {
        await using var connection = new SqlConnection(target);
        await connection.OpenAsync(ct);
        return await SchemaCatalog.ReadAsync(connection, null, 300, ct);
    }
    public DisposableDatabase()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL"));
        builder.InitialCatalog = "master";
        master = builder.ConnectionString;
        builder.InitialCatalog = Name;
        target = builder.ConnectionString;
    }
    public DocAiDbContext Context() => new(new DbContextOptionsBuilder<DocAiDbContext>()
        .UseSqlServer(target, sql => sql.CommandTimeout(300)).Options);
    public async Task Create(CancellationToken ct, bool enableRcsi = true)
    {
        await ExecuteAt(master, $"CREATE DATABASE [{Name}]", ct);
        await ExecuteAt(master, $"ALTER DATABASE [{Name}] SET READ_COMMITTED_SNAPSHOT {(enableRcsi ? "ON" : "OFF")}", ct);
    }
    public Task Execute(string sql, CancellationToken ct) => ExecuteAt(target, sql, ct);
    private static async Task ExecuteAt(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(ct);
    }
    public async Task ExecuteBatches(IEnumerable<string> batches, CancellationToken ct)
    {
        await using var connection = new SqlConnection(target);
        await connection.OpenAsync(ct);
        foreach (var batch in batches)
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 300 };
            await command.ExecuteNonQueryAsync(ct);
        }
    }
    public Task<string> ScalarMaster(string sql, CancellationToken ct) => Scalar(master, sql, ct);
    private static async Task<string> Scalar(string cs, string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };
        return Convert.ToString(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
    public async Task VerifyHistoryAndRcsi(CancellationToken ct, bool baselineOnly = false)
    {
        Assert.That(await ScalarMaster($"SELECT CONVERT(int,is_read_committed_snapshot_on) FROM sys.databases WHERE name=N'{Name}'", ct), Is.EqualTo("1"));
        Assert.That(await Scalar(target, "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory", ct), Is.EqualTo(baselineOnly ? "1" : "3"));
        Assert.That(await Scalar(target,
            "SELECT MigrationId+'|'+ProductVersion FROM dbo.__EFMigrationsHistory WHERE MigrationId='20261101000000_InitialSchema'", ct), Is.EqualTo("20261101000000_InitialSchema|10.0.13"));
    }
    public async Task<string[]> Snapshot(CancellationToken ct)
    {
        await using var connection = new SqlConnection(target);
        await connection.OpenAsync(ct);
        var catalog = await SchemaCatalog.ReadAsync(connection, null, 300, ct, fullSecurity: true);
        var prices = await Scalar(target, "SELECT * FROM dbo.AzureMeterPrices ORDER BY MeterCode,EffectiveFrom FOR JSON PATH,INCLUDE_NULL_VALUES", ct);
        return [.. catalog.Structure, .. catalog.Security, prices];
    }

    public async ValueTask DisposeAsync()
    {
        // Also handles partial CREATE/Migrate failures; only our generated database name is used.
        SqlConnection.ClearAllPools();
        await ExecuteAt(master, $"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END", CancellationToken.None);
    }

}
