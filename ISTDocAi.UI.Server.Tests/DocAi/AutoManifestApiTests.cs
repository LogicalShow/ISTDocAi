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

    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10];

    [TestCase(false)]
    [TestCase(true)]
    public async Task AUTO_with_line_returns_422_before_catalog_or_upload(bool image)
    {
        using var app = new ApiFactory();
        app.Ports.Models.Add(new("AUTO", null, "classify", "router", .8m));
        using var form = IntakeTests.Form(image ? Png : null, type: "AUTO", line: "MAERSK");
        using var response = await app.Client().PostAsync("/api/v2/jobs", form);
        await IntakeTests.Error(response, 422, "INVALID_REQUEST");
        Assert.Empty(app.Ports.Requests);
        Assert.Equal(0, app.Ports.Uploads);
        Assert.Equal(0, app.Ports.CatalogReads);
    }

    [TestCase("png", "image/png", 1)]
    [TestCase("jpg", "image/jpeg", 1)]
    [TestCase("tif", "image/tiff", 2)]
    public async Task AUTO_accepts_images_by_magic_bytes(string extension, string contentType, int pageCount)
    {
        byte[] bytes = extension switch
        {
            "png" => Png,
            "jpg" => [255, 216, 255, 224],
            // Little-endian TIFF with two chained image file directories.
            "tif" => [73, 73, 42, 0, 8, 0, 0, 0, 0, 0, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            _ => throw new ArgumentOutOfRangeException(nameof(extension))
        };
        using var app = new ApiFactory();
        app.Ports.Models.Add(new("AUTO", null, "classify", "router", .8m));
        // The form's PDF filename and media type must not override the bytes.
        using var form = IntakeTests.Form(bytes, type: "AUTO");
        using var response = await app.Client().PostAsync("/api/v2/jobs", form);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(pageCount, (await response.ReadValue<SubmitJobResponse>())!.PageCount);
        Assert.Equal(contentType, app.Ports.UploadedContentType);
        Assert.True(app.Ports.UploadedPath!.EndsWith("." + extension, StringComparison.Ordinal));
        Assert.Equal(new[] { "probe", "cap", "upload", "commit" }, app.Ports.Calls);
        Assert.All(app.Ports.Requests, request =>
        {
            Assert.Equal("AUTO", request.DocumentTypeCode);
            Assert.Null(request.RequestedLine);
            Assert.Equal(contentType, request.ContentType);
            Assert.Equal(pageCount, request.PageCount);
        });
    }

    [Test]
    public async Task Explicit_BL_still_accepts_png()
    {
        using var app = new ApiFactory();
        using var form = IntakeTests.Form(Png, type: "BL");
        using var response = await app.Client().PostAsync("/api/v2/jobs", form);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("image/png", app.Ports.UploadedContentType);
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
