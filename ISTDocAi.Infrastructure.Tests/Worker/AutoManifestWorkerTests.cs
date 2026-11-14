using ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Application.Common.Mappings.DocAi;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ErrorCodes = ISTDocAi.Domain.Common.Constants.DocAi.ErrorCodes;
using System.Text.Json;

using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class AutoManifestWorkerTests
{
    internal static WorkerFakes Auto(string type = "MANIFEST", string? line = null)
    {
        var f = new WorkerFakes();
        f.Job = f.Job with { DocumentTypeCode = "AUTO", Stage = "Classify", PageCount = 2 };
        f.Position = f.Position with { Stage = "Classify", RequestedDocumentTypeCode = "AUTO" };
        f.Models.Clear();
        f.Models.AddRange([new("AUTO", null, "classify", "doc-router-v1", .8m),
            new("BL", null, "extract", "bl-v1", .8m), new("MANIFEST", null, "extract", "manifest-v1", .8m)]);
        f.Mapping = new(line, .8m) { TargetDocumentTypeCode = type };
        f.Class = new(type == "BL" ? "bill-of-lading" : "cargo-manifest", .95);
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "mapping-appsettings.json")).Build();
        f.MappingParser = new AnalysisParser(new InMemoryMappingProfileResolver(Options.Create(config.GetSection("DocAi:Mapping").Get<AnalyzeResultMappingOptions>()!)));
        var json = type == "MANIFEST" ? File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Manifest", "test-A-kestrel-seen.analyze.json")) : WorkerFakes.RawJson;
        f.Polls.Enqueue(new PollResult.Succeeded("classify", f.Clock.GetUtcNow(), 2));
        f.Polls.Enqueue(new PollResult.Succeeded(json, f.Clock.GetUtcNow(), 2));
        f.Raw = json;
        return f;
    }

    
    [TestCase("BL", ResultTypes.BillOfLading)]
    [TestCase("MANIFEST", ResultTypes.Manifest)]
    public async Task AUTO_classifies_then_extracts_and_maps_the_real_result(string type, string resultType)
    {
        var f = Auto(type);
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(new[] { "classify", "extract" }, f.MeterEntries);
        Assert.Equal(type, f.Classification!.ResolvedDocumentTypeCode); Assert.Null(f.Classification.ResolvedLine);
        Assert.Equal(resultType, f.Completion!.ResultType);
        Assert.Matches("^[0-9a-f]{64}$", f.Completion.MappingFingerprint!);
        Assert.Equal(new[] { "post:doc-router-v1", "post:" + (type == "BL" ? "bl-v1" : "manifest-v1") }, f.Calls.Where(c => c.StartsWith("post:", StringComparison.Ordinal)));
        var result = CanonicalResultCodec.Deserialize(f.Completion.ResultJson, resultType);
        Assert.Equal(type, result.DocumentTypeCode);
        if (result is CanonicalManifestResult manifest)
        {
            Assert.Equal(new[] { 2, 4, 3 }, manifest.BillsOfLading.Select(b => b.Containers.Length));
            Assert.Empty(manifest.UnidentifiedBillsOfLading);
            using var truth = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Manifest", "test-A-kestrel-seen.truth.json")));
            var expected = truth.RootElement.GetProperty("Containers").EnumerateArray().ToDictionary(c => c.GetProperty("ContainerNo").GetString()!, c => c.GetProperty("BLNumber").GetString());
            var count = 0;
            foreach (var bill in manifest.BillsOfLading)
                foreach (var container in bill.Containers)
                {
                    Assert.Equal(expected[container.ContainerNumber!.Value!], bill.Fields["billOfLadingNumber"].Value);
                    count++;
                }
            Assert.Equal(9, count);
        }
        using var payload = JsonDocument.Parse(f.Completion.PayloadJson);
        Assert.Equal("AUTO", payload.RootElement.GetProperty("requestedDocumentType").GetString());
        Assert.Equal(type, payload.RootElement.GetProperty("documentType").GetString());
        Assert.Equal(resultType, payload.RootElement.GetProperty("resultType").GetString());
    }

    
    [TestCase("other")] [TestCase("unknown")] [TestCase("missing")]
    public async Task Unsupported_classes_bill_classification_once_without_extraction(string scenario)
    {
        var f = Auto();
        if (scenario == "unknown") f.Mapping = null;
        else if (scenario == "missing") f.Class = new(null, 0);
        else f.Mapping = new(null, .8m) { TargetDocumentTypeCode = null };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(new[] { "classify" }, f.MeterEntries); Assert.Null(f.Completion);
        Assert.Null(f.Classification!.ResolvedDocumentTypeCode);
        using var payload = JsonDocument.Parse(f.Classification.PayloadJson);
        Assert.Equal("UNCLASSIFIED", payload.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Single(f.Calls, c => c.StartsWith("post:", StringComparison.Ordinal));
    }

    
    [TestCase(.799999, false)] [TestCase(.8, true)] [TestCase(.800001, true)]
    [TestCase(double.NaN, false)] [TestCase(double.PositiveInfinity, false)]
    public async Task Class_threshold_is_inclusive_and_nonfinite_confidence_is_rejected_but_billed(double confidence, bool accepted)
    {
        var f = Auto("BL"); f.Class = f.Class with { Confidence = confidence };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(accepted ? "BL" : null, f.Classification!.ResolvedDocumentTypeCode);
        Assert.Equal(accepted, f.Completion is not null);
        Assert.Equal(accepted ? 2 : 1, f.MeterEntries.Count);
    }

    
    [TestCase(null)] [TestCase("LINE")]
    public async Task Missing_exact_extractor_preserves_classification_charge_and_never_falls_back(string? line)
    {
        var f = Auto("BL", line);
        if (line is null) f.Models.RemoveAll(m => m.Kind == "extract" && m.DocumentTypeCode == "BL");
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal("MODEL_NOT_FOUND", f.Failure!.ErrorCode);
        Assert.Equal(new[] { "classify" }, f.MeterEntries);
        Assert.Single(f.Calls, c => c.StartsWith("post:", StringComparison.Ordinal));
    }

    [Test]
    public async Task Crash_immediately_after_S18_resumes_durable_route_without_reclassifying()
    {
        var f = Auto(); using var stop = new CancellationTokenSource();
        f.AfterClassification = stop.Cancel;
        await f.Processor().ExecuteAsync(f.Job, stop.Token);
        Assert.Null(f.Completion); Assert.Equal("MANIFEST", f.Position.ResolvedDocumentTypeCode);
        f.AfterClassification = null;
        // The original claimed object still says Classify and has no resolved type.
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(ResultTypes.Manifest, f.Completion!.ResultType);
        Assert.Equal(new[] { "classify", "extract" }, f.MeterEntries);
        Assert.Single(f.Calls, c => c == "post:doc-router-v1");
        Assert.Single(f.Calls, c => c == "post:manifest-v1");
    }

    [Test]
    public async Task Map_resume_uses_producing_profile_and_durable_type_despite_stale_claim_and_empty_catalog()
    {
        var f = Auto(); f.Polls.Clear(); f.Models.Clear();
        f.Job = f.Job with { ResolvedDocumentTypeCode = "BL", ShippingLineCode = "STALE" };
        f.Position = f.Position with { Stage = "Map", ResolvedDocumentTypeCode = "MANIFEST", ShippingLineCode = null,
            MapOperationId = 9, MapRawResultPath = "raw-path", MapModelId = "manifest-v1" };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(ResultTypes.Manifest, f.Completion!.ResultType);
        Assert.Empty(f.MeterEntries); Assert.DoesNotContain("catalog", f.Calls);
        Assert.Contains("threshold:manifest-v1", f.Calls); Assert.DoesNotContain("post:doc-router-v1", f.Calls);
    }

    [Test]
    public async Task Stale_lease_cannot_commit_a_route_or_classification_charge()
    {
        var f = Auto(); f.Outcomes["classify"] = WriteOutcome.LeaseLost;
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Null(f.Position.ResolvedDocumentTypeCode); Assert.Empty(f.MeterEntries);
        Assert.Null(f.Completion); Assert.Equal("classify", f.Calls.Last());
    }

    [Test]
    public async Task Explicit_request_cannot_switch_document_type()
    {
        var f = Auto(); f.Job = f.Job with { DocumentTypeCode = "BL" };
        f.Position = f.Position with { RequestedDocumentTypeCode = "BL", ResolvedDocumentTypeCode = "BL" };
        f.Models.Add(new("BL", null, "classify", "bl-classifier", .8m));
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Null(f.Classification!.ResolvedDocumentTypeCode); Assert.Null(f.Completion);
        Assert.Equal(new[] { "classify" }, f.MeterEntries);
    }

    [Test]
    public async Task Explicit_BL_still_maps_and_always_supplies_the_result_discriminator()
    {
        var f = new WorkerFakes(); await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(ResultTypes.BillOfLading, f.Completion!.ResultType);
        Assert.Equal("BL", CanonicalResultCodec.Deserialize(f.Completion.ResultJson, f.Completion.ResultType).DocumentTypeCode);
        Assert.DoesNotContain("classify", f.Calls);
    }




    [Test]
    public void Legacy_explicit_types_use_the_producing_profile_and_reject_a_profile_type_mismatch()
    {
        var profile = new AnalyzeResultMappingProfile { DocumentTypeCode = "INV" };
        profile.FieldMap["shipperName"] = "CustomShipper";
        var options = new AnalyzeResultMappingOptions(); options.Models["invoice-model"] = profile;
        var mapper = new ModelResultMapper(new InMemoryMappingProfileResolver(Options.Create(options)));
        const string json = """{"pages":[{}],"documents":[{"fields":{"CustomShipper":{"valueString":"Original text","confidence":0.9}}}]}""";
        var result = mapper.Map(json, "INV", null, "invoice-model", .8);
        Assert.Equal("Original text", Assert.IsType<CanonicalResult>(result.Result).Fields["shipperName"].Value);
        Assert.Equal("INV", result.Result.DocumentTypeCode);
        profile.DocumentTypeCode = "BL";
        Assert.Throws<OptionsValidationException>(() => mapper.Map(json, "INV", null, "invoice-model", .8));
    }

    [Test]
    public async Task Manifest_webhook_is_typed_and_signed_over_the_exact_sent_bytes()
    {
        var f = Auto(); await f.Processor().ExecuteAsync(f.Job, default);
        var webhook = new WebhookFakes();
        webhook.Delivery = webhook.Delivery with { PayloadJson = f.Completion!.PayloadJson };
        webhook.Context = webhook.Context with { ResultJson = f.Completion.ResultJson };
        await webhook.Processor().ExecuteAsync(webhook.Delivery);
        var request = Assert.Single(webhook.Requests);
        Assert.True(WebhookSignature.Verify(request.Timestamp, request.Body, request.Signature, webhook.Key));
        var envelope = JsonSerializer.Deserialize<ISTDocAi.Application.Services.DocAi.Webhooks.WebhookBody>(request.Body, new ISTDocAi.Infrastructure.Services.DocAi.Webhooks.DocAiV2Json().Options)!.Job;
        Assert.Equal("AUTO", envelope.RequestedDocumentType); Assert.Equal("MANIFEST", envelope.DocumentType);
        Assert.Equal(ResultTypes.Manifest, envelope.ResultType); Assert.IsType<CanonicalManifestResult>(envelope.Result);
    }
    [Test]
    public async Task Oversized_manifest_keeps_the_webhook_limit_and_records_BODY_TOO_LARGE_without_sending()
    {
        var f = Auto(); await f.Processor().ExecuteAsync(f.Job, default);
        var manifest = Assert.IsType<CanonicalManifestResult>(CanonicalResultCodec.Deserialize(f.Completion!.ResultJson, ResultTypes.Manifest));
        manifest.Header["vesselName"] = new() { Value = new string('x', DeliverWebhook.MaximumBodyBytes + 1024), Confidence = .99, Page = 1 };
        var webhook = new WebhookFakes();
        webhook.Delivery = webhook.Delivery with { PayloadJson = f.Completion.PayloadJson };
        webhook.Context = webhook.Context with { ResultJson = CanonicalResultCodec.Serialize(manifest) };
        await webhook.Processor().ExecuteAsync(webhook.Delivery);
        Assert.Empty(webhook.Requests); Assert.Equal("BODY_TOO_LARGE", Assert.Single(webhook.Attempts).Error);
    }
}
