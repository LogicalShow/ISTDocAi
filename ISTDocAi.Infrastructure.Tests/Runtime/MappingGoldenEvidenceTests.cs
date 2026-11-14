using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public sealed class MappingGoldenEvidenceTests
{
    [TestCase("effective.json")]
    [TestCase("sql-resolver.json")]
    public void Frozen_baseline_matches_appsettings_and_cold_SQL_row_seam(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fingerprints", file)));
        var app = new InMemoryMappingProfileResolver(Microsoft.Extensions.Options.Options.Create(ManifestResultTests.Options()));
        foreach (var item in json.RootElement.EnumerateArray())
        {
            var model = item.GetProperty("model").GetString()!;
            var type = item.GetProperty(file == "effective.json" ? "type" : "documentType").GetString()!;
            var expected = item.GetProperty("expected").GetString()!;
            var profile = app.Resolve(model, type);
            var rows = new List<SqlMappingProfileResolver.Row>();
            void Add(string kind, Dictionary<string,string> map)
            {
                foreach (var pair in map) rows.Add(new() { ModelId = model, DocumentTypeCode = type,
                    MappingShape = profile is AnalyzeResultMappingProfile ? "ModelProfile" : "LegacyFallback",
                    ContainersField = profile.ContainersField, ContainerNumberField = profile.ContainerNumberField,
                    SealNumberField = profile.SealNumberField, BillsOfLadingField = (profile as AnalyzeResultMappingProfile)?.BillsOfLadingField ?? "BillsOfLading",
                    MapKind = kind, CanonicalKey = pair.Key, AzureName = pair.Value });
            }
            Add("Field", profile.FieldMap); Add("Container", profile.ContainerFieldMap);
            if (profile is AnalyzeResultMappingProfile selected) { Add("Header", selected.HeaderFieldMap); Add("Bill", selected.BillFieldMap); }
            var loads = 0;
            var sql = new SqlMappingProfileResolver(TimeProvider.System, _ => { loads++; return rows.ToArray(); });
            Assert.That(MappingFingerprint.ComputeEffective(profile, type), Is.EqualTo(expected));
            Assert.That(MappingFingerprint.ComputeEffective(sql.Resolve(model, type), type), Is.EqualTo(expected));
            Assert.That(MappingFingerprint.ComputeEffective(sql.Resolve(model, type), type), Is.EqualTo(expected));
            Assert.That(loads, Is.EqualTo(1));
        }
    }
}
