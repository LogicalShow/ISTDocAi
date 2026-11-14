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
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ISTDocAi.UI.Server.Tests.DocAi;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
internal sealed class ModelMappingTests : GuardedDeploymentTests
{
    private static readonly Dictionary<string, string> Expected = new()
    {
        ["billOfLadingNumber"] = "BillOfLadingNumber", ["shipperName"] = "ShipperName",
        ["consigneeName"] = "ConsigneeName", ["vesselName"] = "VesselName",
        ["voyageNumber"] = "VoyageNumber", ["portOfLoading"] = "PortOfLoading",
        ["portOfDischarge"] = "PortOfDischarge", ["issueDate"] = "DateOfIssue"
    };

    [Test]
    public void Model_strings_and_containers_map_with_real_host_options()
    {
        var options = new InMemoryMappingProfileResolver().Resolve("legacy", "BL");
        var parsed = AnalyzeResultParser.Parse(ModelResult(), "BL", "MAERSK", "bl-maersk-v1", options);
        Assert.Equal(8, parsed.Fields.Count);
        foreach (var (canonical, model) in Expected)
        {
            var field = parsed.Fields[canonical]!;
            Assert.Equal(model == "DateOfIssue" ? "14-Sep-2026" : model + " value", field.Value);
            Assert.Equal(.97, field.Confidence);
            Assert.Equal(1, field.Page);
        }
        Assert.Equal("14-Sep-2026", parsed.Fields["issueDate"]!.Value); // String field: no date normalization.
        Assert.Equal(2, parsed.Containers.Length);
        Assert.Equal("CSQU3054383", parsed.Containers[0].ContainerNumber.Value);
        Assert.Equal("SEAL001", parsed.Containers[0].SealNumber!.Value);
        Assert.Equal("MSCU6639870", parsed.Containers[1].ContainerNumber.Value);
        Assert.Null(parsed.Containers[1].SealNumber);
    }

    [Test]
    public void Default_mapping_finds_no_fields_or_containers_in_this_model()
    {
        var parsed = AnalyzeResultParser.Parse(ModelResult(), "BL", "MAERSK", "bl-maersk-v1");
        Assert.Empty(parsed.Fields);
        Assert.Empty(parsed.Containers);
    }

    private static object Field(string value) => new
    {
        type = "string", valueString = value, content = value, confidence = .97,
        boundingRegions = new[] { new { pageNumber = 1, polygon = new[] { 0, 0, 1, 0, 1, 1, 0, 1 } } }
    };

    private static string ModelResult()
    {
        var fields = Expected.Values.ToDictionary(name => name,
            name => Field(name == "DateOfIssue" ? "14-Sep-2026" : name + " value"));
        fields.Add("BookingNumber", Field("BOOK001"));
        fields.Add("TotalGrossWeightKg", Field("25000"));
        fields.Add("Containers", new { type = "array", valueArray = new[] {
            Container("CSQU3054383", "SEAL001"), Container("MSCU6639870", null) } });
        return JsonSerializer.Serialize(new {
            apiVersion = "2024-11-30", modelId = "bl-maersk-v1", content = "Synthetic model output",
            pages = new[] { new { pageNumber = 1 } },
            documents = new[] { new { docType = "bl-maersk-v1", confidence = .98, fields } }
        });
    }

    private static object Container(string number, string? seal)
    {
        var fields = new Dictionary<string, object> {
            ["ContainerNo"] = Field(number), ["GrossWeightKg"] = Field("12500"),
            ["MeasurementCbm"] = Field("30"), ["Packages"] = Field("100"), ["Type"] = Field("40HC")
        };
        if (seal is not null) fields.Add("SealNo", Field(seal));
        return new { type = "object", valueObject = fields };
    }

    private sealed class MappingFactory(string? environmentPrefix = null) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Development");
            if (environmentPrefix is not null)
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddEnvironmentVariables(environmentPrefix));
        }
    }
}
