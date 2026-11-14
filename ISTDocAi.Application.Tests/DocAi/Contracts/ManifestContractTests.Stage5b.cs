using NUnit.Framework;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Application.Tests.DocAi.Contracts;
public sealed partial class ManifestContractTests
{
    [TestCase("test-A-kestrel-seen", 0)]
    [TestCase("test-B-sahel-unseen", 2)]
    [TestCase("test-A-kestrel-seen.v2", 0)]
    public void Mapper_output_validates_and_source_generated_JSON_round_trips(string name, int unidentifiedCount)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "mapping-appsettings.json")).Build();
        var options = configuration.GetSection("DocAi:Mapping").Get<AnalyzeResultMappingOptions>()!;
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Manifest", name + ".analyze.json"));
        var result = new ModelResultMapper(new ISTDocAi.Infrastructure.Tests.DocAi.Runtime.InMemoryMappingProfileResolver(Options.Create(options))).MapManifest(json, null, name.EndsWith(".v2", StringComparison.Ordinal) ? "manifest-v2" : "manifest-v1", .8).Value!;
        var contract = ModelResultMapper.ToContract(result);
        var serialized = JsonSerializer.SerializeToNode(contract, ContractJson.Options)!;
        using var stream = typeof(CanonicalManifestResult).Assembly.GetManifestResourceStream("DocAi.Contracts.Schemas.canonical-manifest-result-v1.schema.json")!;
        using var reader = new StreamReader(stream);
        var schema = JsonSchema.FromText(reader.ReadToEnd());
        Assert.That(schema.Evaluate(serialized).IsValid, Is.True);
        Assert.That(contract.ContractVersion, Is.EqualTo("1.0"));
        var roundTrip = JsonSerializer.Deserialize(serialized, ContractJsonContext.Default.CanonicalManifestResult)!;
        Assert.That(JsonNode.DeepEquals(serialized, JsonSerializer.SerializeToNode(roundTrip, ContractJson.Options)), Is.True);
        Assert.That(roundTrip.BillsOfLading[0].Containers[0].ContainerNumber, Is.Not.Null);
        Assert.That(roundTrip.UnidentifiedBillsOfLading, Has.Length.EqualTo(unidentifiedCount));
        if (name == "test-B-sahel-unseen")
            Assert.That(roundTrip.UnidentifiedBillsOfLading.Any(b => b.Fields.GetValueOrDefault("shipperName")?.Value == "DELTA PRODUCE LOGISTICS S.A.E."), Is.True);
        var unidentified = serialized["unidentifiedBillsOfLading"]!.DeepClone();
        serialized.AsObject().Remove("unidentifiedBillsOfLading");
        Assert.That(schema.Evaluate(serialized).IsValid, Is.False);
        serialized["unidentifiedBillsOfLading"] = new JsonArray();
        Assert.That(schema.Evaluate(serialized).IsValid, Is.True);
        serialized["unidentifiedBillsOfLading"] = unidentified;
        if (unidentified.AsArray().Count > 0)
        {
        var sourceIndex = unidentified[0]!["sourceIndex"]!.DeepClone();
        unidentified[0]!["sourceIndex"] = -1;
        Assert.That(schema.Evaluate(serialized).IsValid, Is.False);
        unidentified[0]!["sourceIndex"] = sourceIndex;
        }
        serialized["billsOfLading"]![0]!["containers"]![0]!["grossWeight"]!["confidence"] = 2;
        Assert.That(schema.Evaluate(serialized).IsValid, Is.False);
    }
}
