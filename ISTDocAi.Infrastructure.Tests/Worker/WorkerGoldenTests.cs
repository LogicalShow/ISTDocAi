using System.Text.Json;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class WorkerGoldenTests
{
    [TestCase("bl")]
    [TestCase("manifest")]
    [TestCase("auto")]
    [TestCase("unclassified")]
    [TestCase("lease-loss")]
    [TestCase("takeover")]
    [TestCase("retry")]
    [TestCase("mapping-failure")]
    [TestCase("model-changed")]
    public async Task Trace_matches_baseline(string name)
    {
        var f = name is "auto" or "unclassified" or "manifest" ? AutoManifestWorkerTests.Auto("MANIFEST", null) : new WorkerFakes();
        f.Job = f.Job with { JobId = Guid.Parse("11111111-1111-4111-8111-111111111111"), LeaseToken = Guid.Parse("33333333-3333-4333-8333-333333333333"), CorrelationId = "baseline-correlation-1" };
        if (name == "manifest") { f.Job = f.Job with { DocumentTypeCode = "MANIFEST", Stage = "Extract" }; f.Position = f.Position with { Stage = "Extract", RequestedDocumentTypeCode = "MANIFEST", ResolvedDocumentTypeCode = "MANIFEST" }; f.Polls.Dequeue(); }
        if (name == "unclassified") f.Mapping = null;
        if (name == "retry") f.Submit = new SubmitResult.Rejected(503, "FAKE_CODE", null, null);
        if (name == "mapping-failure") f.ParseError = true;
        if (name == "lease-loss") f.Outcomes["ledger"] = WriteOutcome.LeaseLost;
        if (name == "takeover") { f.Accepted(); f.Job = f.Job with { TakeoverCount = 1 }; }
        if (name == "model-changed") { f.RunReason = "ModelChanged"; f.ChangesJson = """{"extractModel":{"oldModelId":"bl-v0","newModelId":"bl-v1"},"mapping":true}"""; }
        await f.Processor().ExecuteAsync(f.Job, default);
        var actual = JsonSerializer.SerializeToNode(new { calls = f.Calls, meterEntries = f.MeterEntries, retrySeconds = f.RetrySeconds, retryError = f.RetryError,
            complete = f.Completion is not null, failure = f.Failure?.ErrorCode, classification = f.Classification?.ResolvedDocumentTypeCode,
            result = f.Completion?.ResultJson, logs = f.Logger.Lines.Select(l => new { level = l.Level.ToString(), text = l.Text }) });
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Stage8", name + ".json")));
        Assert.That(JsonNode.DeepEquals(actual, expected), Is.True, name);
    }
}
