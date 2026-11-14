using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ISTDocAi.Infrastructure.Tests.DocAi;
public sealed partial class MappingProfilesTests
{
    private sealed record Seed(string ModelId, string DocumentTypeCode, MappingShape MappingShape,
        string ContainersField, string ContainerNumberField, string SealNumberField,
        string BillsOfLadingField, MappingFieldRow[] Rows);

    private static readonly Dictionary<string, string> Hashes = new()
    {
        ["bl-maersk-v1"] = "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556",
        ["bl-v2"] = "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556",
        ["bl-v3"] = "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556",
        ["manifest-v1"] = "87aa5155640c0e10dc2379621e36c290a7bc16905d6e5f201cd55466f3a8ef2d",
        ["manifest-v2"] = "9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480",
        ["manifest-v3"] = "9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480",
        ["legacy"] = "c9a1b76d2f79ff08355d51d2d347238d803d5370f8c9a21988b27ba578ed0d17"
    };

    private static Seed[] Seeds()
    {
        using var stream = typeof(DocAiDbContext).Assembly.GetManifestResourceStream("DocAi.Database.Migrations.Sql.ModelMappingSeeds.json");
        Assert.NotNull(stream);
        return JsonSerializer.Deserialize<Seed[]>(stream, new JsonSerializerOptions
        { Converters = { new JsonStringEnumConverter() } })!;
    }

    [Test]
    public void Frozen_seed_constants_hydrate_to_stage_one_hashes_and_fit_SQL_limits()
    {
        Assert.Equal(7, Seeds().Length);
        foreach (var seed in Seeds())
        {
            Assert.InRange(seed.Rows.Length, 1, 256);
            foreach (var row in seed.Rows)
            {
                Assert.Matches("^[A-Za-z][A-Za-z0-9_]{0,63}$", row.CanonicalKey);
                Assert.Matches("^[A-Za-z0-9_.-]{1,128}$", row.AzureName);
            }
            foreach (var name in new[] { seed.ContainersField, seed.ContainerNumberField, seed.SealNumberField, seed.BillsOfLadingField })
                Assert.Matches("^[A-Za-z0-9_.-]{1,128}$", name);
            Assert.Equal(Hashes[seed.ModelId], Fingerprint(seed, seed.Rows));
        }
    }
    private static string Fingerprint(Seed seed, IEnumerable<MappingFieldRow> rows) => MappingFingerprint.ComputeEffective(
        MappingProfileHydrator.Hydrate(new(seed.ModelId, seed.DocumentTypeCode, seed.MappingShape,
            seed.ContainersField, seed.ContainerNumberField, seed.SealNumberField, seed.BillsOfLadingField), rows), seed.DocumentTypeCode);

    [Test, Category("Sql")]
    public async Task Fresh_migration_round_trips_all_frozen_profiles()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
        await using var db = new DisposableDatabase();
        await Latest(db, ct);
        Assert.Equal("7", await db.ScalarTarget("SELECT COUNT(*) FROM dbo.MappingProfiles", ct));
        Assert.Equal("0", await db.ScalarTarget("SELECT COUNT(*) FROM dbo.ModelMappingBindings", ct));
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        foreach (var seed in Seeds())
        {
            await using var command = new SqlCommand("""
                SELECT p.DocumentTypeCode,p.MappingShape,p.ContainersField,p.ContainerNumberField,p.SealNumberField,p.BillsOfLadingField,
                  f.MapKind,f.CanonicalKey,f.AzureName FROM dbo.MappingProfiles p JOIN dbo.MappingFields f ON f.ProfileId=p.ProfileId
                WHERE p.ProfileCode=@code
                """, connection);
            command.Parameters.AddWithValue("@code", seed.ModelId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<MappingFieldRow>();
            Seed? actual = null;
            while (await reader.ReadAsync(ct))
            {
                actual ??= new(seed.ModelId, reader.GetString(0), Enum.Parse<MappingShape>(reader.GetString(1)),
                    reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), []);
                rows.Add(new(Enum.Parse<MappingMapKind>(reader.GetString(6)), reader.GetString(7), reader.GetString(8)));
            }
            Assert.NotNull(actual);
            Assert.Equal(Hashes[seed.ModelId], Fingerprint(actual, rows));
        }
        await Latest(db, ct);
        await db.VerifyHistoryAndRcsi(ct);
    }
    [Test, Category("Sql")]
    public async Task Registry_shapes_bind_only_extractors_and_copy_implicit_fallback()
    {
        SqlTestGuard.Require();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
        foreach (var local in new[] { true, false })
        {
            await using var db = new DisposableDatabase();
            await Baseline(db, ct);
            var models = local ? new[] { "bl-maersk-v1", "bl-v2", "bl-v3", "manifest-v1", "manifest-v2", "manifest-v3", "extra-bl" }
                : ["bl-v3", "manifest-v3"];
            var version = 0;
            foreach (var model in models)
                await db.Execute($"INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('{model}','extract','{(model.StartsWith("manifest", StringComparison.Ordinal) ? "MANIFEST" : "BL")}',{++version},'Draft')", ct);
            await db.Execute("INSERT dbo.ModelRegistry(ModelId,Kind,DocumentTypeCode,Version,Status) VALUES ('IST-Classifier-v6','classify','AUTO',1,'Draft'),('doc-router-v1','classify','AUTO',2,'Draft')", ct);
            await db.Execute("UPDATE dbo.ModelRegistry SET Status='Active',ActivatedAtUtc=SYSUTCDATETIME() WHERE ModelId IN ('bl-v3','manifest-v3','IST-Classifier-v6')", ct);
            await Latest(db, ct);
            Assert.Equal("2", await db.ScalarTarget("SELECT COUNT(*) FROM dbo.MappingProfiles p JOIN dbo.ModelMappingBindings b ON b.ProfileId=p.ProfileId WHERE b.ModelId IN ('bl-v3','manifest-v3') AND p.SealedAtUtc IS NOT NULL", ct));
            Assert.Equal(models.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), await db.ScalarTarget("SELECT COUNT(*) FROM dbo.ModelMappingBindings", ct));
            Assert.Equal("0", await db.ScalarTarget("SELECT COUNT(*) FROM dbo.ModelMappingBindings WHERE ModelId IN ('IST-Classifier-v6','doc-router-v1')", ct));
            if (local)
            {
                var copied = await db.ScalarTarget("SELECT MapKind,CanonicalKey,AzureName FROM dbo.MappingFields WHERE ProfileId=(SELECT ProfileId FROM dbo.ModelMappingBindings WHERE ModelId='extra-bl') FOR JSON PATH", ct);
                var rows = JsonSerializer.Deserialize<MappingFieldRow[]>(copied, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;
                Assert.Equal(Hashes["legacy"], Fingerprint(Seeds().Single(s => s.ModelId == "legacy"), rows));
                Assert.Equal("0", await db.ScalarTarget("""
                    SELECT COUNT(*) FROM (
                      SELECT MapKind,CanonicalKey,AzureName FROM dbo.MappingFields WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='legacy')
                      EXCEPT SELECT MapKind,CanonicalKey,AzureName FROM dbo.MappingFields WHERE ProfileId=(SELECT ProfileId FROM dbo.ModelMappingBindings WHERE ModelId='extra-bl')
                    ) x
                    """, ct));
            }
        }
    }

}
