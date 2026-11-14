using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class MappingProfileParityTests
{
    // Captured with the pre-seam MappingFingerprint.Compute and host configuration binder.
    public static List<object[]> GoldenProfiles => new()
    {
        new object[] { "bl-maersk-v1", "BL", "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556" },
        new object[] { "bl-v2", "BL", "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556" },
        new object[] { "bl-v3", "BL", "5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556" },
        new object[] { "manifest-v1", "MANIFEST", "87aa5155640c0e10dc2379621e36c290a7bc16905d6e5f201cd55466f3a8ef2d" },
        new object[] { "manifest-v2", "MANIFEST", "9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480" },
        new object[] { "manifest-v3", "MANIFEST", "9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480" },
        new object[] { "legacy", "BL", "c9a1b76d2f79ff08355d51d2d347238d803d5370f8c9a21988b27ba578ed0d17" },
    };

    [Test]
    public void Golden_inventory_covers_every_configured_model()
    {
        Assert.Equal(ManifestResultTests.Options().Models.Keys.Order(StringComparer.Ordinal),
            GoldenProfiles.Select(row => (string)row[0]).Where(model => model != "legacy").Order(StringComparer.Ordinal));
    }

    [TestCaseSource(nameof(GoldenProfiles))]
    public void Real_configuration_hash_and_row_round_trip_are_frozen(string model, string type, string expected)
    {
        var options = ManifestResultTests.Options();
        var resolver = new InMemoryMappingProfileResolver(Options.Create(options));
        var effective = resolver.Resolve(model, type);
        var (header, rows) = ToRows(effective, model, type);
        Assert.Equal(expected, InMemoryMappingProfileResolver.Fingerprint(options, model, type));
        foreach (var ordered in new[] { rows, rows.Reverse().ToArray(), rows.OrderBy(r => r.AzureName, StringComparer.Ordinal).ToArray() })
        {
            var hydrated = MappingProfileHydrator.Hydrate(header, ordered);
            Assert.Equal(effective.GetType(), hydrated.GetType());
            Assert.Equal(expected, MappingFingerprint.ComputeEffective(hydrated, type));
            var fixtures = type == "BL" ? new[] { BillFixture(effective) } :
                new[] { "test-A-kestrel-seen", "test-B-sahel-unseen", "test-A-kestrel-seen.v2" }
                    .Select(n => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Manifest", n + ".analyze.json")));
            foreach (var json in fixtures)
            {
                var before = new AnalysisParser(resolver).Map(json, type, null, model, .8);
                var afterResolver = new FixedResolver(hydrated);
                var after = new AnalysisParser(afterResolver).Map(json, type, null, model, .8);
                Assert.Equal(1, afterResolver.Calls);
                if (type == "BL")
                {
                    var bill = Assert.IsType<ISTDocAi.Application.Common.Contracts.DocAi.V1.CanonicalResult>(after.Result);
                    Assert.NotEmpty(bill.Fields);
                    Assert.NotEmpty(bill.Containers);
                }
                Assert.Equal(before.MappingFingerprint, after.MappingFingerprint);
                Assert.Equal(before.FlaggedFields, after.FlaggedFields);
                Assert.Equal(before.ResultType, after.ResultType);
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(before.Result, before.Result.GetType(), ISTDocAi.Application.Common.Contracts.DocAi.V1.ContractJson.Options),
                    JsonSerializer.SerializeToNode(after.Result, after.Result.GetType(), ISTDocAi.Application.Common.Contracts.DocAi.V1.ContractJson.Options)));
            }
        }
    }

    [TestCaseSource(nameof(GoldenProfiles))]
    public void Every_effective_map_key_value_and_structural_member_is_hashed(string model, string type, string expected)
    {
        var profile = new InMemoryMappingProfileResolver(Options.Create(ManifestResultTests.Options())).Resolve(model, type);
        var (header, rows) = ToRows(profile, model, type);
        void Changed(MappingProfileHeader h, IEnumerable<MappingFieldRow> r) =>
            Assert.NotEqual(expected, MappingFingerprint.ComputeEffective(MappingProfileHydrator.Hydrate(h, r), type));
        Changed(header with { ContainersField = header.ContainersField + "x" }, rows);
        Changed(header with { ContainerNumberField = header.ContainerNumberField + "x" }, rows);
        Changed(header with { SealNumberField = header.SealNumberField + "x" }, rows);
        foreach (var row in rows)
        {
            Changed(header, rows.Select(r => r == row ? r with { AzureName = r.AzureName + "x" } : r));
            Changed(header, rows.Select(r => r == row ? r with { CanonicalKey = r.CanonicalKey + "x" } : r));
            Changed(header, rows.Where(r => r != row));
        }
        foreach (var kind in Enum.GetValues<MappingMapKind>().Where(k => profile is AnalyzeResultMappingProfile || k is MappingMapKind.Field or MappingMapKind.Container))
            Changed(header, rows.Append(new(kind, "extra", "Extra")));
        if (profile is AnalyzeResultMappingProfile)
            Changed(header with { BillsOfLadingField = header.BillsOfLadingField + "x" }, rows);
        else
            Changed(header with { MappingShape = MappingShape.ModelProfile }, rows);
        Assert.NotEqual(expected, MappingFingerprint.ComputeEffective(profile, type + "x"));
        Assert.NotEqual(expected, MappingFingerprint.ComputeEffective(profile, type, "1"));
        Assert.Equal("1.0", MappingFingerprint.CanonicalContractVersion);
        Assert.Equal("2", MappingFingerprint.MappingAlgorithmVersion);
    }

    [Test]
    public void Empty_effective_rows_replace_defaults_and_invalid_rows_fail_without_partial_results()
    {
        var header = new MappingProfileHeader("m", "BL", MappingShape.LegacyFallback);
        var empty = MappingProfileHydrator.Hydrate(header, []);
        Assert.Empty(empty.FieldMap);
        Assert.Empty(empty.ContainerFieldMap);
        Assert.Equal("containers", empty.ContainersField);
        Assert.Equal("containerNumber", empty.ContainerNumberField);
        Assert.Equal("sealNumber", empty.SealNumberField);
        Assert.Throws<ArgumentException>(() => MappingProfileHydrator.Hydrate(header, [new(MappingMapKind.Header, "x", "X")]));
        Assert.Throws<ArgumentException>(() => MappingProfileHydrator.Hydrate(header, [new(MappingMapKind.Field, "x", "X"), new(MappingMapKind.Field, "x", "Y")]));
        Assert.Throws<ArgumentException>(() => MappingProfileHydrator.Hydrate(header with { MappingShape = (MappingShape)99 }, []));
        Assert.Throws<ArgumentException>(() => MappingProfileHydrator.Hydrate(header with { DocumentTypeCode = "MANIFEST" }, []));
        Assert.Throws<ArgumentNullException>(() => MappingProfileHydrator.Hydrate(header, null!));
        Assert.Throws<ArgumentNullException>(() => MappingProfileHydrator.Hydrate(null!, []));
        var separate = MappingProfileHydrator.Hydrate(header, [new(MappingMapKind.Field, "x", "X"), new(MappingMapKind.Container, "x", "Y"), new(MappingMapKind.Field, "X", "Z")]);
        Assert.Equal(2, separate.FieldMap.Count);
        Assert.Equal("Y", separate.ContainerFieldMap["x"]);
        Assert.Empty(empty.FieldMap);
    }

    // Reuse the existing AnalyzeResult fixture, translating its lower-case Azure names to
    // the real configured model's source names so parity exercises populated output.
    private static string BillFixture(AnalyzeResultMappingOptions profile)
    {
        var json = JsonNode.Parse(AnalyzeResultParserTests.Result)!;
        var fields = json["documents"]![0]!["fields"]!.AsObject();
        foreach (var (canonical, source) in profile.FieldMap)
            if (canonical != source && fields.Remove(canonical, out var field)) fields[source] = field;
        var containers = fields["containers"]!;
        fields.Remove("containers");
        fields[profile.ContainersField] = containers;
        foreach (var item in containers["valueArray"]!.AsArray())
        {
            var obj = item!["valueObject"]!.AsObject();
            foreach (var (canonical, fallback) in new[] { ("containerNumber", profile.ContainerNumberField), ("sealNumber", profile.SealNumberField) })
            {
                var source = profile.ContainerFieldMap.GetValueOrDefault(canonical, fallback);
                if (canonical != source && obj.Remove(canonical, out var field)) obj[source] = field;
            }
        }
        return json.ToJsonString();
    }

    [Test]
    public void Dependency_injection_constructs_both_consumers_without_connections_or_constructor_ambiguity()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ISqlConnectionFactory, NoConnections>();
        builder.Services.AddDocAiSql(o => o.ConnectionString = "unused");
        builder.Services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new ConfigurationBuilder().Build());
        builder.Services.AddDocAiMapping();
        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<SqlMappingProfileResolver>(provider.GetRequiredService<IMappingProfileResolver>());
        Assert.IsType<AnalysisParser>(provider.GetRequiredService<IAnalysisParser>());
        Assert.NotNull(provider.GetRequiredService<ModelResultMapper>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IJobRepository>());
    }

    private sealed class NoConnections : ISqlConnectionFactory
    {
        public Task<Microsoft.Data.SqlClient.SqlConnection> OpenAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This pure test must never open a database connection.");
    }

    private static (MappingProfileHeader Header, MappingFieldRow[] Rows) ToRows(AnalyzeResultMappingOptions profile, string model, string type)
    {
        var selected = profile as AnalyzeResultMappingProfile;
        var header = new MappingProfileHeader(model, type, selected is null ? MappingShape.LegacyFallback : MappingShape.ModelProfile,
            profile.ContainersField, profile.ContainerNumberField, profile.SealNumberField, selected?.BillsOfLadingField ?? "BillsOfLading");
        var rows = new List<MappingFieldRow>();
        void Add(MappingMapKind kind, Dictionary<string, string> map) => rows.AddRange(map.Select(p => new MappingFieldRow(kind, p.Key, p.Value)));
        Add(MappingMapKind.Field, profile.FieldMap);
        Add(MappingMapKind.Container, profile.ContainerFieldMap);
        if (selected is not null) { Add(MappingMapKind.Header, selected.HeaderFieldMap); Add(MappingMapKind.Bill, selected.BillFieldMap); }
        return (header, rows.ToArray());
    }

    private sealed class FixedResolver(AnalyzeResultMappingOptions profile) : IMappingProfileResolver
    {
        public int Calls { get; private set; }
        public AnalyzeResultMappingOptions Resolve(string modelId, string documentType, bool allowLegacyDocumentType = false)
        {
            Assert.Equal(0, Calls++);
            return profile;
        }
    }
}
