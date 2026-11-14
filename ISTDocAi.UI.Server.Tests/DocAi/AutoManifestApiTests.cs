using NUnit.Framework;
using Assert = ISTDocAi.UI.Server.Tests.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;



namespace ISTDocAi.UI.Server.Tests.DocAi;

public sealed class AutoManifestApiTests
{
    [Test]
    public async Task AUTO_PDF_requires_an_active_classifier_and_is_listed_only_through_it()
    {
        using var missing = new ApiFactory();
        // An invalid AUTO extractor must not accidentally enable AUTO intake or catalog entries.
        missing.Ports.Models.Add(new("AUTO", null, "extract", "wrong-kind", .8m));
        var rejected = await missing.Client().PostAsync("/api/v2/jobs", IntakeTests.Form(type: "AUTO"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal("NOT_IN_CATALOG", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Error").GetProperty("Code").GetString());
        Assert.Empty(missing.Ports.Requests); Assert.Equal(0, missing.Ports.Uploads);
        var catalog = (await missing.Client().GetValue<CatalogResponse>("/api/v2/catalog"))!;
        Assert.DoesNotContain(catalog.Items, c => c.DocumentTypeCode == "AUTO");
        Assert.DoesNotContain(catalog.AutoDetect, c => c.DocumentTypeCode == "AUTO");
        using var active = new ApiFactory(); active.Ports.Models.Add(new("AUTO", null, "classify", "doc-router-v1", .8m));
        var accepted = await active.Client().PostAsync("/api/v2/jobs", IntakeTests.Form(type: "AUTO"));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.All(active.Ports.Requests, r => { Assert.Equal("AUTO", r.DocumentTypeCode); Assert.Null(r.RequestedLine); });
        catalog = (await active.Client().GetValue<CatalogResponse>("/api/v2/catalog"))!;
        Assert.Contains(catalog.AutoDetect, c => c.DocumentTypeCode == "AUTO");
    }

    [Test]
    public async Task AUTO_with_line_returns_422_before_catalog_or_upload()
    {
        using var app = new ApiFactory();
        var response = await app.Client().PostAsync("/api/v2/jobs", IntakeTests.Form(type: "AUTO", line: "MAERSK"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(app.Ports.Requests); Assert.Equal(0, app.Ports.Uploads); Assert.Equal(0, app.Ports.CatalogReads);
    }

    [Test]
    public async Task AUTO_rejects_images_but_explicit_BL_keeps_its_existing_file_policy()
    {
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10];
        using var app = new ApiFactory(); app.Ports.Models.Add(new("AUTO", null, "classify", "router", .8m));
        var response = await app.Client().PostAsync("/api/v2/jobs", IntakeTests.Form(png, type: "AUTO"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode); Assert.Empty(app.Ports.Requests);
        response = await app.Client().PostAsync("/api/v2/jobs", IntakeTests.Form(png, type: "BL"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); Assert.Equal("image/png", app.Ports.UploadedContentType);
    }


    [Test]
    public async Task GET_large_manifest_is_not_subject_to_the_webhook_body_limit()
    {
        using var app = new ApiFactory();
        var result = CanonicalResultCodec.Serialize(Manifest(new string('x', (1024 * 1024) + 1024)));
        app.Ports.Run = new(app.Ports.JobId, 1, "large-request", "Completed", null, null, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, 2, "1.0", result, 0) {
            RequestedDocumentTypeCode = "AUTO", ResolvedDocumentTypeCode = "MANIFEST", ResultType = ResultTypes.Manifest };
        var response = await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}?runNo=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ReadSurfaceTests.AssertCanonical(app, response);
        var bytes = await response.Content.ReadAsByteArrayAsync(); Assert.True(bytes.Length > (1024 * 1024));
        var envelope = JsonDocument.Parse(bytes).RootElement.GetProperty("Value").Deserialize<JobResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal((1024 * 1024) + 1024, Assert.IsType<CanonicalManifestResult>(envelope.Result).Header["vesselName"].Value!.Length);
        var payload = "{\"eventType\":\"job.completed\",\"requestedDocumentType\":\"AUTO\",\"documentType\":\"MANIFEST\",\"resultType\":\"cargo-manifest.v1\",\"result\":null}";
        Assert.True(Encoding.UTF8.GetByteCount(payload) + Encoding.UTF8.GetByteCount(result) > (1024 * 1024));
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task GET_older_run_uses_its_manifest_metadata_and_supports_historical_discriminator_fallback(bool historical)
    {
        using var app = new ApiFactory();
        app.Ports.Run = new(app.Ports.JobId, 1, "old-request", "Completed", null, null, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, 2, "1.0", CanonicalResultCodec.Serialize(Manifest()), 0) {
            RequestedDocumentTypeCode = "AUTO", ResolvedDocumentTypeCode = "MANIFEST", ResultType = historical ? null : ResultTypes.Manifest };
        var response = await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}?runNo=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ReadSurfaceTests.AssertCanonical(app, response);
        var envelope = (await response.ReadValue<JobResponse>())!;
        Assert.Equal("AUTO", envelope.RequestedDocumentType); Assert.Equal("MANIFEST", envelope.DocumentType);
        Assert.Equal(ResultTypes.Manifest, envelope.ResultType); Assert.IsType<CanonicalManifestResult>(envelope.Result);
    }
    [Test]
    public async Task Stored_run_and_result_type_disagreement_is_rejected()
    {
        using var app = new ApiFactory();
        app.Ports.Run = new(app.Ports.JobId, 1, "request", "Completed", null, null, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, 2, "1.0", CanonicalResultCodec.Serialize(Manifest()), 0) {
            RequestedDocumentTypeCode = "AUTO", ResolvedDocumentTypeCode = "BL", ResultType = ResultTypes.Manifest };
        var response = await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}?runNo=1");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        ReadSurfaceTests.AssertNoPayloadLogs(app);
    }
    [Test]
    public async Task Unknown_result_discriminator_is_rejected_without_exposing_saved_result()
    {
        using var app = new ApiFactory();
        app.Ports.Run = new(app.Ports.JobId, 1, "request", "Completed", null, null, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, 2, "1.0", CanonicalResultCodec.Serialize(Manifest("PRIVATE_VALUE")), 0) {
            RequestedDocumentTypeCode = "AUTO", ResolvedDocumentTypeCode = "MANIFEST", ResultType = "unknown.v1" };
        var response = await app.Client().GetAsync($"/api/v2/jobs/{app.Ports.JobId}?runNo=1");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        ReadSurfaceTests.AssertNoPayloadLogs(app);
        Assert.DoesNotContain("PRIVATE_VALUE", await response.Content.ReadAsStringAsync());
    }
    internal static CanonicalManifestResult Manifest(string? description = null) => new()
    {
        ContractVersion = "1.0", DocumentTypeCode = "MANIFEST", ShippingLineCode = null, ModelId = "manifest-v1", PageCount = 2,
        Header = description is null ? [] : new() { ["vesselName"] = new() { Value = description, Page = 1, Confidence = .99 } },
        BillsOfLading = [], UnidentifiedBillsOfLading = [], UnassignedContainers = [], Flags = []
    };

}
