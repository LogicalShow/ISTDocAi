using ISTDocAi.Domain.Common.Constants.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Analysis;
using ISTDocAi.Infrastructure.Services.DocAi.Storage;
using ISTDocAi.Infrastructure.Services.DocAi.Identity;
using ISTDocAi.Infrastructure.Services.DocAi.Secrets;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class AnalyzeResultParserTests
{
    [Test]
    public void Spaced_container_maps_through_model_mapper_with_original_text()
    {
        const string json = """{"pages":[{},{}],"documents":[{"fields":{"containers":{"valueArray":[{"valueObject":{"containerNumber":{"valueString":"UACU 5636100","content":"UACU 5636100","confidence":0.99,"boundingRegions":[{"pageNumber":2}]}}}]}}}]}""";
        var mapper = new ModelResultMapper(new InMemoryMappingProfileResolver(Microsoft.Extensions.Options.Options.Create(new AnalyzeResultMappingOptions())));
        var result = mapper.MapBillOfLading(json, null, "m", .8).Value!;
        var number = PortAssert.Single(result.Containers).ContainerNumber;
        PortAssert.Equal("UACU5636100", number.Value);
        PortAssert.Equal("UACU 5636100", number.RawText);
        PortAssert.Equal(.99, number.Confidence);
        PortAssert.Equal(2, number.Page);
        PortAssert.DoesNotContain(result.Flags, f => f.Reason == FlagReasons.CheckDigit);
    }

    internal const string Result = """
    {
      "apiVersion":"2024-11-30", "modelId":"ocean-bl", "content":"example document",
      "pages":[{"pageNumber":1,"width":8.5,"height":11},{"pageNumber":2}],
      "documents":[{
        "docType":"ocean-bl", "confidence":0.97,
        "fields":{
          "billOfLadingNumber":{"type":"string","valueString":"BL123","content":"BL 123","confidence":0.99,"boundingRegions":[{"pageNumber":2,"polygon":[0,0,1,0,1,1,0,1]}]},
          "shipperName":{"type":"string","valueString":"Example shipper","content":"Example shipper","confidence":0.95},
          "issueDate":{"type":"date","valueDate":"2026-10-25","content":"25 October 2026","confidence":0.93,"boundingRegions":[{"pageNumber":1}]},
          "Weight":{"type":"number","valueNumber":1234.5,"content":"1,234.5","confidence":0.88},
          "Count":{"type":"integer","valueInteger":42,"confidence":0.9},
          "vesselName":{"type":"string","content":"Example vessel","confidence":0.91},
          "containers":{"type":"array","valueArray":[
            {"type":"object","valueObject":{
              "containerNumber":{"type":"string","valueString":"CSQU3054383","confidence":0.99,"boundingRegions":[{"pageNumber":2}]},
              "sealNumber":{"type":"string","valueString":"SEAL001","confidence":0.96}}},
            {"type":"object","valueObject":{
              "containerNumber":{"type":"string","valueString":"MSCU6639870","confidence":0.94}}}
          ]}
        }
      },{"docType":"ignored","confidence":0.1,"fields":{"consigneeName":{"valueString":"ignored"}}}]
    }
    """;
    [Test]
    public void Maps_realistic_result_and_uses_only_first_document()
    {
        var result = AnalyzeResultParser.Parse(Result, "BL", "OCEAN", "ocean-bl");
        PortAssert.Equal(2, result.PageCount); PortAssert.Equal("BL", result.DocumentTypeCode);
        PortAssert.Equal("OCEAN", result.ShippingLineCode); PortAssert.Equal("ocean-bl", result.ModelId);
        var number = result.Fields["billOfLadingNumber"]!;
        PortAssert.Equal("BL123", number.Value); PortAssert.Equal("BL 123", number.RawText); PortAssert.Equal(.99, number.Confidence); PortAssert.Equal(2, number.Page);
        PortAssert.Null(result.Fields["shipperName"]!.RawText); PortAssert.Equal(1, result.Fields["shipperName"]!.Page);
        PortAssert.Equal("2026-10-25", result.Fields["issueDate"]!.Value);
        PortAssert.Equal("Example vessel", result.Fields["vesselName"]!.Value); PortAssert.Null(result.Fields["vesselName"]!.RawText);
        PortAssert.False(result.Fields.ContainsKey("consigneeName")); PortAssert.False(result.Fields.ContainsKey("portOfLoading"));
        PortAssert.Equal(2, result.Containers.Length); PortAssert.Equal("SEAL001", result.Containers[0].SealNumber!.Value);
        PortAssert.Null(result.Containers[1].SealNumber); PortAssert.Equal(1, result.Containers[1].ContainerNumber.Page);
        PortAssert.True(MappingServices.Bill.Map(result, .8).IsSuccess);
    }
    [Test]
    public void Custom_map_numbers_are_invariant()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var result = AnalyzeResultParser.Parse(Result, "BL", null, "model", new()
            { FieldMap = new() { ["weight"] = "Weight", ["count"] = "Count", ["missing"] = "Absent" } });
            PortAssert.Equal("1234.5", result.Fields["weight"]!.Value); PortAssert.Equal("42", result.Fields["count"]!.Value);
            PortAssert.Equal(2, result.Fields.Count);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }
    [Test]
    public void No_pages_drops_unlocated_fields_and_containers()
    {
        var result = AnalyzeResultParser.Parse("""{"pages":[],"documents":[{"fields":{"shipperName":{"content":"text"},"containers":{"valueArray":[{"valueObject":{"containerNumber":{"content":"number"}}}]}}}]}""", "BL", null, "model");
        PortAssert.Equal(0, result.PageCount); PortAssert.Empty(result.Fields); PortAssert.Empty(result.Containers);
    }
    
    [TestCase("{\"pages\":[],\"documents\":[]}")]
    [TestCase("{}")]
    public void Empty_document_is_not_invented(string json)
    {
        var result = AnalyzeResultParser.Parse(json, "BL", null, "model");
        PortAssert.Empty(result.Fields); PortAssert.Empty(result.Containers); PortAssert.Equal(0, result.PageCount);
        PortAssert.Null(AnalyzeResultParser.ParseClassifier(json).DocType);
    }
    [Test]
    public void Classifier_reads_first_doc_type_and_confidence()
    {
        var result = AnalyzeResultParser.ParseClassifier(Result);
        PortAssert.Equal("ocean-bl", result.DocType); PortAssert.Equal(.97, result.Confidence);
    }
    [Test]
    public void Configurable_container_names()
    {
        var result = AnalyzeResultParser.Parse("""{"pages":[{}],"documents":[{"fields":{"Boxes":{"valueArray":[{"valueObject":{"Number":{"valueString":"CSQU3054383"},"Seal":{"valueString":"S1"}}}]}}}]}""", "BL", null, "model",
            new() { ContainersField = "Boxes", ContainerNumberField = "Number", SealNumberField = "Seal" });
        PortAssert.Equal("CSQU3054383", PortAssert.Single(result.Containers).ContainerNumber.Value);
        PortAssert.Equal("S1", result.Containers[0].SealNumber!.Value);
    }
    [Test]
    public void Raw_path_is_stable_for_retries()
    {
        var job = Guid.NewGuid(); const long operation = 123;
        PortAssert.Equal($"results/{job:D}/2/{operation}.json", BlobRawResultStore.PathFor(job, 2, operation));
        PortAssert.Throws<ArgumentException>(() => BlobRawResultStore.PathFor(job, 0, operation));
    }
    
    [TestCase("{\"type\":\"string\",\"confidence\":0.3}")]
    [TestCase("{\"type\":\"string\",\"valueString\":\"\",\"confidence\":0.3}")]
    [TestCase("{\"type\":\"string\",\"valueString\":\"  \",\"content\":\" \",\"confidence\":0.3}")]
    public void Empty_neural_fields_do_not_create_scalars_containers_or_seals(string empty)
    {
        var json = """
        {"pages":[{}],"documents":[{"fields":{
          "shipperName":EMPTY,
          "containers":{"valueArray":[
            {"valueObject":{"containerNumber":EMPTY,"sealNumber":{"valueString":"ignored"}}},
            {"valueObject":{"containerNumber":{"valueString":"CSQU3054383","confidence":0.99},"sealNumber":EMPTY}}
          ]}
        }}]}
        """.Replace("EMPTY", empty);
        var parsed = AnalyzeResultParser.Parse(json, "BL", null, "model");
        PortAssert.Empty(parsed.Fields);
        PortAssert.Null(PortAssert.Single(parsed.Containers).SealNumber);
        var mapped = MappingServices.Bill.Map(parsed, .8);
        PortAssert.True(mapped.IsSuccess); PortAssert.Empty(mapped.Value!.Flags);
    }
    [Test]
    public void Whitespace_value_with_content_is_found_on_fallback_page()
    {
        var parsed = AnalyzeResultParser.Parse("""{"pages":[{}],"documents":[{"fields":{"shipperName":{"valueString":"  ","content":"Found text","confidence":0.9}}}]}""", "BL", null, "model");
        var field = parsed.Fields["shipperName"]!;
        PortAssert.Equal("Found text", field.Value); PortAssert.Equal(1, field.Page);
    }
    
    [TestCase("valueDate", "\"unexpected date\"")]
    [TestCase("valueDate", "123")]
    [TestCase("valueDate", "{}")]
    [TestCase("valueString", "123")]
    [TestCase("valueString", "{}")]
    [TestCase("valueNumber", "1e100")]
    [TestCase("valueNumber", "\"123\"")]
    [TestCase("valueInteger", "1e100")]
    [TestCase("valueInteger", "{}")]
    public void Malformed_field_preserves_content_without_failing_other_fields(string property, string value)
    {
        var json = """
        {"pages":[{}],"documents":[{"fields":{
          "issueDate":{"PROPERTY":VALUE,"content":"Unparsed text","confidence":0.7},
          "shipperName":{"valueString":"Retained","confidence":0.9}
        }}]}
        """.Replace("PROPERTY", property).Replace("VALUE", value);
        var parsed = AnalyzeResultParser.Parse(json, "BL", null, "model");
        PortAssert.Null(parsed.Fields["issueDate"]!.Value);
        PortAssert.Equal("Unparsed text", parsed.Fields["issueDate"]!.RawText);
        PortAssert.Equal("Retained", parsed.Fields["shipperName"]!.Value);
        PortAssert.Equal(1, parsed.Fields["issueDate"]!.Page);
        // Without content the malformed field is not found at all.
        parsed = AnalyzeResultParser.Parse(json.Replace(",\"content\":\"Unparsed text\"", ""), "BL", null, "model");
        PortAssert.False(parsed.Fields.ContainsKey("issueDate"));
    }
    [Test]
    public void Malformed_content_and_metadata_do_not_throw_for_a_field()
    {
        var parsed = AnalyzeResultParser.Parse("""
        {"pages":[{}],"documents":[{"fields":{
          "shipperName":{"valueString":"Found","content":{},"confidence":{},"boundingRegions":[{"pageNumber":"wrong"}]},
          "consigneeName":{"content":{}},"issueDate":null
        }}]}
        """, "BL", null, "model");
        var field = PortAssert.Single(parsed.Fields).Value!;
        PortAssert.Equal("Found", field.Value); PortAssert.Null(field.RawText); PortAssert.Equal(0, field.Confidence); PortAssert.Equal(1, field.Page);
    }
    [Test]
    public void Malformed_json_still_throws()
    {
        PortAssert.ThrowsAny<System.Text.Json.JsonException>(() => AnalyzeResultParser.Parse("{", "BL", null, "model"));
    }
}
