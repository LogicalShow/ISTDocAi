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
using System.Runtime.InteropServices;

using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class ReviewRegressionTests
{
    
    [TestCase(429, "AZURE_THROTTLED")] [TestCase(408, "AZURE_UNAVAILABLE")]
    [TestCase(500, "AZURE_UNAVAILABLE")] [TestCase(503, "AZURE_UNAVAILABLE")] [TestCase(599, "AZURE_UNAVAILABLE")]
    public async Task Retryable_Azure_status_keeps_reason_on_queue_only(int status, string expected)
    {
        foreach (var poll in new[] { false, true })
        {
            var f = new WorkerFakes();
            if (poll) { f.Accepted(); f.Polls.Enqueue(new PollResult.HttpError(status, TimeSpan.FromSeconds(45))); }
            else f.Submit = new SubmitResult.Rejected(status, "AzureCode", "PRIVATE_TOKEN", TimeSpan.FromSeconds(45));
            await f.Processor().ExecuteAsync(f.Job, default);
            AssertRetry(f, expected); Assert.Equal(45, f.RetrySeconds);
        }
    }
    
    [TestCase("post")] [TestCase("poll")] [TestCase("sending")]
    public async Task Ambiguous_or_missing_Azure_answer_records_unavailable(string scenario)
    {
        var f = new WorkerFakes();
        if (scenario == "post") f.Submit = new SubmitResult.NoAnswer();
        else { f.Accepted(); if (scenario == "poll") f.Polls.Enqueue(new PollResult.NoAnswer()); else f.Position = f.Position with { State = "Sending" }; }
        await f.Processor().ExecuteAsync(f.Job, default); AssertRetry(f, ErrorCodes.AzureUnavailable);
    }
    
    [TestCase("parse")] [TestCase("map")] [TestCase("classifier")]
    public async Task Parse_and_canonical_mapping_errors_record_mapping_failed(string scenario)
    {
        var f = new WorkerFakes { ParseError = scenario != "map", MapRejected = scenario == "map" };
        if (scenario == "classifier") f.Position = f.Position with { Stage = "Classify" };
        await f.Processor().ExecuteAsync(f.Job, default); AssertRetry(f, ErrorCodes.MappingFailed);
    }
    
    [TestCase("original")] [TestCase("save")] [TestCase("read")]
    public async Task Blob_read_and_write_errors_record_storage_unavailable(string scenario)
    {
        var f = new WorkerFakes { StorageError = scenario == "original", StorageFailureAt = scenario };
        await f.Processor().ExecuteAsync(f.Job, default); AssertRetry(f, ErrorCodes.StorageUnavailable);
        Assert.DoesNotContain("raw-delete", f.Calls);
    }
    [Test]
    public async Task Other_exceptions_still_record_internal_error()
    {
        var f = new WorkerFakes { AfterAccepted = () => throw new InvalidOperationException("PRIVATE_TOKEN") };
        await f.Processor().ExecuteAsync(f.Job, default); AssertRetry(f, ErrorCodes.InternalError);
    }
    
    [TestCase("azure-throttled")] [TestCase("azure-unavailable")] [TestCase("mapping")]
    [TestCase("storage")] [TestCase("internal")]
    public async Task Fifth_failure_never_exposes_queued_diagnostic_in_terminal_error_or_event(string scenario)
    {
        var f = new WorkerFakes(); f.Job = f.Job with { Attempts = 4 };
        switch (scenario)
        {
            case "azure-throttled": f.Submit = new SubmitResult.Rejected(429, null, null, null); break;
            case "azure-unavailable": f.Submit = new SubmitResult.NoAnswer(); break;
            case "mapping": f.ParseError = true; break;
            case "storage": f.StorageError = true; break;
            default: f.AfterAccepted = () => throw new InvalidOperationException("PRIVATE_TOKEN"); break;
        }
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Null(f.RetryError); Assert.Equal(ErrorCodes.RetriesExhausted, f.Failure!.ErrorCode);
        Assert.True(f.Failure.CountsAsAttempt); Assert.Contains("RETRIES_EXHAUSTED", f.Failure.PayloadJson);
        foreach (var code in new[] { ErrorCodes.AzureThrottled, ErrorCodes.AzureUnavailable, ErrorCodes.MappingFailed, ErrorCodes.StorageUnavailable })
            Assert.DoesNotContain(code, f.Failure.PayloadJson);
    }
    [Test]
    public async Task Local_GET_throttling_waits_without_poll_driven_renewals_or_attempt()
    {
        var f = new WorkerFakes(); f.Accepted();
        f.Polls.Enqueue(new AnalysisUnavailableException(AnalysisUnavailableReason.RateLimited));
        f.Polls.Enqueue(new AnalysisUnavailableException(AnalysisUnavailableReason.RateLimited));
        var task = f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(new[] { "position", "poll" }, f.Calls);
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        await Until(() => f.Calls.Count(c => c == "poll") == 2 && f.Clock.Delays.Count == 3);
        Assert.False(task.IsCompleted);
        f.Clock.Advance(TimeSpan.FromSeconds(1)); await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain("renew", f.Calls); Assert.NotNull(f.Completion); Assert.Null(f.RetryError);
    }
    [Test]
    public async Task Submit_reuses_original_stream_buffer_and_passes_only_document_length()
    {
        var f = new WorkerFakes(); await f.Processor().ExecuteAsync(f.Job, default);
        Assert.True(MemoryMarshal.TryGetArray(f.SubmittedBytes, out var submitted));
        Assert.Same(f.OriginalDestination!.GetBuffer(), submitted.Array);
        Assert.Equal(0, submitted.Offset); Assert.Equal("PRIVATE_FILE_CONTENT".Length, submitted.Count);
        Assert.True(submitted.Array!.Length > submitted.Count);
    }
    private static void AssertRetry(WorkerFakes f, string code)
    {
        Assert.Equal(code, f.RetryError); Assert.Null(f.Failure); Assert.Null(f.Completion);
        foreach (var line in f.Logger.Lines)
            foreach (var secret in new[] { "PRIVATE_TOKEN", "PRIVATE_FIELD_VALUE", "PRIVATE_FILE.pdf", "PRIVATE_FILE_CONTENT" }) Assert.DoesNotContain(secret, line.Text);
    }
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(1, timeout.Token);
    }
}
