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

using ISTDocAi.Domain.Dtos.DocAi;
using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class ProcessJobTests
{
    [Test]
    public async Task Extraction_orders_raw_save_ledger_azure_delete_map_complete_raw_delete_and_serializes_contract()
    {
        var f = new WorkerFakes();
        await f.Processor().ExecuteAsync(f.Job, default);
        AssertOrder(f.Calls, "reserve", "begin", "post:bl-v1", "accepted", "poll", "raw-save", "ledger", "azure-delete", "raw-read", "threshold:bl-v1", "parse", "complete", "raw-delete");
        var completed = Assert.IsType<Completion>(f.Completion);
        using var result = JsonDocument.Parse(completed.ResultJson);
        Assert.Equal("1.0", result.RootElement.GetProperty("contractVersion").GetString());
        Assert.Equal("PRIVATE_FIELD_VALUE", result.RootElement.GetProperty("fields").GetProperty("shipperName").GetProperty("value").GetString());
        Assert.Equal(1, completed.FlaggedFields);
        AssertEnvelope(completed.PayloadJson, completed.EventId, "Completed", null);
        AssertPrivate(f);
    }
    [Test]
    public async Task Webhook_keeps_the_run_reason_and_changes()
    {
        var f = new WorkerFakes { RunReason = "ModelChanged", ChangesJson = """{"classifier":{"oldModelId":"c1","newModelId":"c2"}}""" };
        await f.Processor().ExecuteAsync(f.Job, default);
        using var json = JsonDocument.Parse(f.Completion!.PayloadJson);
        Assert.Equal("ModelChanged", json.RootElement.GetProperty("runReason").GetString());
        Assert.Equal("c2", json.RootElement.GetProperty("changes").GetProperty("classifier").GetProperty("newModelId").GetString());
    }

    
    [TestCase(0, 30)] [TestCase(1, 60)] [TestCase(2, 120)] [TestCase(3, 240)] [TestCase(4, -1)]
    public async Task Retry_policy_uses_prior_attempts_and_fifth_failure_is_terminal(int attempts, int delay)
    {
        var f = new WorkerFakes { Submit = new SubmitResult.Rejected(503, "code", "PRIVATE_FIELD_VALUE", null) };
        f.Job = f.Job with { Attempts = (byte)attempts };
        await f.Processor().ExecuteAsync(f.Job, default);
        if (delay < 0) { Assert.Equal(ErrorCodes.RetriesExhausted, f.Failure!.ErrorCode); Assert.True(f.Failure.CountsAsAttempt); }
        else Assert.Equal(delay, f.RetrySeconds);
        AssertOrder(f.Calls, "close-Failed", delay < 0 ? "fail" : "retry");
        AssertPrivate(f);
    }
    
    [TestCase(400, "INVALID_DOCUMENT")] [TestCase(404, "MODEL_NOT_FOUND")]
    [TestCase(401, "AZURE_AUTH")] [TestCase(403, "AZURE_AUTH")]
    [TestCase(408, "retry")] [TestCase(429, "retry")] [TestCase(500, "retry")]
    [TestCase(502, "retry")] [TestCase(599, "retry")]
    [TestCase(409, "INVALID_DOCUMENT")] [TestCase(422, "INVALID_DOCUMENT")]
    public async Task Rejected_status_closes_call_then_classifies_without_logging_Azure_text(int status, string expected)
    {
        var f = new WorkerFakes { Submit = new SubmitResult.Rejected(status, new string('X', 80), "PRIVATE_TOKEN", TimeSpan.FromSeconds(45.1)) };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Contains("close-Failed", f.Calls);
        Assert.Equal(50, f.ClosedAzureCode!.Length);
        if (expected == "retry") Assert.Equal(46, f.RetrySeconds);
        else if (expected == "AZURE_AUTH") { Assert.Equal((300, (string?)"AZURE_AUTH"), f.Released); Assert.Equal(1, f.OpenCount); Assert.Null(f.RetrySeconds); }
        else { Assert.Equal(expected, f.Failure!.ErrorCode); Assert.False(f.Failure.CountsAsAttempt); AssertEnvelope(f.Failure.PayloadJson, f.Failure.EventId, "Failed", expected); }
        if (status == 404) Assert.Contains(f.Logger.Lines, l => l.Level == LogLevel.Error && l.Text.Contains("registry.drift"));
        AssertPrivate(f);
    }
    
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public async Task Takeover_budget_allows_two_and_fails_third_without_attempt(int count)
    {
        var f = new WorkerFakes(); f.Job = f.Job with { TakeoverCount = (byte)count };
        await f.Processor().ExecuteAsync(f.Job, default);
        if (count >= 3) { Assert.Equal(ErrorCodes.WorkerCrashLoop, f.Failure!.ErrorCode); Assert.False(f.Failure.CountsAsAttempt); Assert.DoesNotContain("position", f.Calls); }
        else Assert.NotNull(f.Completion);
        Assert.Equal(count > 0, f.Logger.Lines.Any(l => l.Text.Contains("job.taken_over")));
    }
    [Test]
    public async Task Accepted_resumes_polling_without_original_or_POST()
    {
        var f = new WorkerFakes(); f.Accepted();
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.NotNull(f.Completion); Assert.DoesNotContain("original", f.Calls); Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Sending_closes_unknown_and_backs_off_or_rereads_when_202_won_race(bool race)
    {
        var f = new WorkerFakes(); f.Accepted(); f.Position = f.Position with { State = "Sending", OperationLocation = null };
        if (race) { f.Outcomes["close-Unknown"] = WriteOutcome.LeaseLost; f.AfterClose = () => f.Accepted(); }
        await f.Processor().ExecuteAsync(f.Job, default);
        if (race) { Assert.NotNull(f.Completion); Assert.Null(f.RetrySeconds); Assert.Equal(2, f.Calls.Count(c => c == "position")); }
        else { Assert.Equal(30, f.RetrySeconds); Assert.Contains(f.Logger.Lines, l => l.Level == LogLevel.Warning && l.Text.Contains("operation.unknown")); }
        Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
    }
    [Test]
    public async Task Two_unknown_calls_fail_without_a_third_POST()
    {
        var f = new WorkerFakes(); f.Position = f.Position with { UnknownInRun = 2 };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(ErrorCodes.RetriesExhausted, f.Failure!.ErrorCode); Assert.False(f.Failure.CountsAsAttempt);
        Assert.DoesNotContain("begin", f.Calls);
    }
    [Test]
    public async Task Map_resume_uses_producing_retired_model_threshold_without_analysis_or_second_ledger()
    {
        var f = new WorkerFakes { Threshold = .8m }; f.Models.Clear();
        f.Position = f.Position with { Stage = "Map", MapOperationId = 1, MapRawResultPath = "raw-path", MapModelId = "retired-model" };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Contains("threshold:retired-model", f.Calls); Assert.Equal(0, f.Completion!.FlaggedFields);
        Assert.DoesNotContain("ledger", f.Calls); Assert.DoesNotContain("catalog", f.Calls);
    }
    [Test]
    public async Task Model_missing_is_fatal_before_original_read()
    {
        var f = new WorkerFakes(); f.Models.Clear(); await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(ErrorCodes.ModelNotFound, f.Failure!.ErrorCode); Assert.DoesNotContain("original", f.Calls);
    }
    
    [TestCase(true)] [TestCase(false)]
    public async Task Missing_original_fails_and_other_storage_errors_retry(bool missing)
    {
        var f = new WorkerFakes { Missing = missing, StorageError = !missing }; await f.Processor().ExecuteAsync(f.Job, default);
        if (missing) Assert.Equal(ErrorCodes.OriginalMissing, f.Failure!.ErrorCode); else Assert.Equal(30, f.RetrySeconds);
        Assert.DoesNotContain("reserve", f.Calls); AssertPrivate(f);
    }
    
    [TestCase(AnalysisUnavailableReason.RateLimited)] [TestCase(AnalysisUnavailableReason.CredentialUnavailable)]
    public async Task Reserve_unavailable_releases_without_recording_call_or_attempt(AnalysisUnavailableReason reason)
    {
        var f = new WorkerFakes { Reserve = new AnalysisUnavailableException(reason) }; await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(reason == AnalysisUnavailableReason.RateLimited ? (10, (string?)null) : (300, "AZURE_AUTH"), f.Released);
        Assert.DoesNotContain("begin", f.Calls); Assert.Null(f.RetrySeconds);
    }
    [Test]
    public async Task Submit_no_answer_closes_unknown_then_retries()
    {
        var f = new WorkerFakes { Submit = new SubmitResult.NoAnswer() }; await f.Processor().ExecuteAsync(f.Job, default);
        AssertOrder(f.Calls, "post:bl-v1", "close-Unknown", "retry"); Assert.Equal(30, f.RetrySeconds);
    }
    [Test]
    public async Task No_answer_racing_with_saved_202_rereads_and_polls()
    {
        var f = new WorkerFakes { Submit = new SubmitResult.NoAnswer() }; f.Outcomes["close-Unknown"] = WriteOutcome.LeaseLost; f.AfterClose = () => f.Accepted();
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.NotNull(f.Completion); Assert.Null(f.RetrySeconds); Assert.Single(f.Calls, c => c.StartsWith("post:"));
    }
    
    [TestCase(401)] [TestCase(403)] [TestCase(408)] [TestCase(429)] [TestCase(500)] [TestCase(503)] [TestCase(599)] [TestCase(404)]
    public async Task Poll_http_errors_renew_then_release_retry_or_fail_without_closing_as_Sending(int status)
    {
        var f = new WorkerFakes(); f.Accepted(); f.Polls.Enqueue(new PollResult.HttpError(status, TimeSpan.FromSeconds(91.2)));
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Contains("poll", f.Calls);
        Assert.DoesNotContain("renew", f.Calls);
        if (status is 401 or 403) Assert.Equal((300, (string?)"AZURE_AUTH"), f.Released);
        else if (status == 404) { Assert.Equal(ErrorCodes.AnalysisResultMissing, f.Failure!.ErrorCode); Assert.Equal(1, f.Failure.OperationId); }
        else Assert.Equal(92, f.RetrySeconds);
        Assert.DoesNotContain(f.Calls, c => c.StartsWith("close-"));
    }
    
    [TestCase("not-found")] [TestCase("failed")] [TestCase("no-answer")]
    public async Task Poll_final_errors_preserve_operation_atomic_failure_or_resume(string answer)
    {
        var f = new WorkerFakes(); f.Accepted();
        f.Polls.Enqueue(answer switch { "not-found" => new PollResult.NotFound(), "failed" => new PollResult.Failed("azure-code", "PRIVATE_TOKEN"), _ => (object)new PollResult.NoAnswer() });
        await f.Processor().ExecuteAsync(f.Job, default);
        if (answer == "no-answer") Assert.Equal(30, f.RetrySeconds);
        else { Assert.Equal(1, f.Failure!.OperationId); Assert.Equal(answer == "failed" ? ErrorCodes.AnalysisFailed : ErrorCodes.AnalysisResultMissing, f.Failure.ErrorCode); }
        AssertPrivate(f);
    }
    
    [TestCase(2)] [TestCase(300)] [TestCase(301)]
    public async Task Running_renews_waits_at_most_five_minutes_or_releases_without_attempt(int seconds)
    {
        var f = new WorkerFakes(); f.Accepted(); f.Polls.Enqueue(new PollResult.Running(TimeSpan.FromSeconds(seconds)));
        var task = f.Processor().ExecuteAsync(f.Job, default);
        if (seconds <= 300) { Assert.False(task.IsCompleted); Assert.Contains(TimeSpan.FromSeconds(seconds), f.Clock.Delays); f.Clock.Advance(TimeSpan.FromSeconds(seconds)); }
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        if (seconds > 300) { Assert.Equal((seconds, (string?)null), f.Released); Assert.Null(f.RetrySeconds); }
        else Assert.NotNull(f.Completion);
    }
    
    [TestCase(AnalysisUnavailableReason.RateLimited)] [TestCase(AnalysisUnavailableReason.CredentialUnavailable)]
    public async Task Poll_local_admission_failure_waits_one_second_or_opens_breaker_without_attempt(AnalysisUnavailableReason reason)
    {
        var f = new WorkerFakes(); f.Accepted(); f.Polls.Enqueue(new AnalysisUnavailableException(reason));
        var task = f.Processor().ExecuteAsync(f.Job, default);
        if (reason == AnalysisUnavailableReason.RateLimited) { Assert.Contains(TimeSpan.FromSeconds(1), f.Clock.Delays); f.Clock.Advance(TimeSpan.FromSeconds(1)); }
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        if (reason == AnalysisUnavailableReason.RateLimited) Assert.NotNull(f.Completion); else Assert.Equal((300, (string?)"AZURE_AUTH"), f.Released);
        Assert.Null(f.RetrySeconds);
    }
    
    [TestCase("mapped")] [TestCase("unmapped")] [TestCase("low")] [TestCase("empty")]
    public async Task Classifier_records_charge_and_resolves_line_or_fails_in_same_write(string scenario)
    {
        var f = new WorkerFakes(); f.Job = f.Job with { Stage = "Classify" }; f.Position = f.Position with { Stage = "Classify" };
        if (scenario == "unmapped") f.Mapping = null;
        if (scenario == "low") f.Class = f.Class with { Confidence = .899 };
        if (scenario == "empty") f.Class = new(null, 0);
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.NotNull(f.Classification);
        if (scenario == "mapped") { Assert.Equal("LINE", f.Classification.ResolvedLine); Assert.Contains("post:line-model", f.Calls); Assert.NotNull(f.Completion); AssertOrder(f.Calls, "classify", "post:line-model"); }
        else { Assert.Null(f.Classification.ResolvedLine); Assert.Null(f.Completion); Assert.DoesNotContain("fail", f.Calls); AssertEnvelope(f.Classification.PayloadJson, f.Classification.EventId, "Failed", "UNCLASSIFIED"); }
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Parsing_or_mapping_failure_retries_from_Map_with_cost_already_recorded(bool mapping)
    {
        var f = new WorkerFakes { ParseError = !mapping, MapRejected = mapping };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal("Map", f.Position.Stage); Assert.Equal(30, f.RetrySeconds); AssertOrder(f.Calls, "ledger", "parse", "retry"); Assert.DoesNotContain("raw-delete", f.Calls); AssertPrivate(f);
    }
    
    [TestCase("begin")] [TestCase("accepted")] [TestCase("ledger")] [TestCase("complete")]
    [TestCase("classify")] [TestCase("fail")] [TestCase("retry")] [TestCase("release")] [TestCase("close-Failed")]
    public async Task Lease_lost_at_each_write_stops_without_later_mutation(string write)
    {
        var f = new WorkerFakes(); f.Outcomes[write] = WriteOutcome.LeaseLost;
        if (write == "classify") f.Position = f.Position with { Stage = "Classify" };
        if (write == "fail") f.Models.Clear();
        if (write is "retry" or "close-Failed") f.Submit = new SubmitResult.Rejected(503, "code", null, null);
        if (write == "release") f.Reserve = new AnalysisUnavailableException(AnalysisUnavailableReason.RateLimited);
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.Equal(write, f.Calls.Last());
        Assert.Contains(f.Logger.Lines, l => l.Level == LogLevel.Warning && l.Text.Contains("job.lease_lost"));
        if (write == "begin") Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
    }
    [Test]
    public async Task Sending_close_zero_with_lost_lease_stops_after_renewal_and_does_not_reread()
    {
        var f = new WorkerFakes(); f.Accepted(); f.Position = f.Position with { State = "Sending" };
        f.Outcomes["close-Unknown"] = WriteOutcome.LeaseLost; f.Outcomes["renew"] = WriteOutcome.LeaseLost;
        await f.Processor().ExecuteAsync(f.Job, default); Assert.Equal("renew", f.Calls.Last()); Assert.Single(f.Calls, c => c == "position"); Assert.Null(f.RetrySeconds);
    }
    
    [TestCase("begin")] [TestCase("ledger")] [TestCase("complete")]
    public async Task Operation_not_open_stops_like_lease_loss(string write)
    {
        var f = new WorkerFakes(); f.Outcomes[write] = WriteOutcome.OperationNotOpen;
        await f.Processor().ExecuteAsync(f.Job, default); Assert.Equal(write, f.Calls.Last()); Assert.Null(f.RetrySeconds);
    }
    [Test]
    public async Task Begin_unknown_budget_fails_without_POST()
    {
        var f = new WorkerFakes(); f.Outcomes["begin"] = WriteOutcome.RetriesExhausted;
        await f.Processor().ExecuteAsync(f.Job, default); Assert.Equal(ErrorCodes.RetriesExhausted, f.Failure!.ErrorCode); Assert.DoesNotContain(f.Calls, c => c.StartsWith("post:"));
    }
    [Test]
    public async Task Best_effort_deletion_failures_do_not_block_completion_or_leak_exception_text()
    {
        var f = new WorkerFakes { DeleteError = true }; await f.Processor().ExecuteAsync(f.Job, default);
        Assert.NotNull(f.Completion); Assert.Null(f.RetrySeconds); Assert.Contains("raw-delete", f.Calls); AssertPrivate(f);
    }
    [Test]
    public async Task Shutdown_cancels_poll_wait_and_releases_immediately_without_attempt()
    {
        var f = new WorkerFakes(); f.Accepted(); f.Polls.Enqueue(new PollResult.Running(TimeSpan.FromMinutes(1)));
        using var stop = new CancellationTokenSource(); var task = f.Processor().ExecuteAsync(f.Job, stop.Token);
        Assert.False(task.IsCompleted); stop.Cancel(); await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((0, (string?)null), f.Released); Assert.Null(f.RetrySeconds); Assert.Single(f.Calls, c => c == "poll");
    }
    [Test]
    public async Task Shutdown_does_not_cancel_inflight_POST_or_following_Accepted_write_then_releases()
    {
        var f = new WorkerFakes(); using var stop = new CancellationTokenSource();
        var sent = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<SubmitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.SubmitHandler = ct => { sent.SetResult(ct); return response.Task; };
        var task = f.Processor().ExecuteAsync(f.Job, stop.Token); var postToken = await sent.Task;
        stop.Cancel(); Assert.False(postToken.IsCancellationRequested); Assert.False(task.IsCompleted);
        response.SetResult(new SubmitResult.Accepted("https://test/operations/1", null)); await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(f.AcceptedToken.IsCancellationRequested);
        AssertOrder(f.Calls, "accepted", "release"); Assert.Equal((0, (string?)null), f.Released); Assert.DoesNotContain("poll", f.Calls);
    }
    [Test]
    public async Task Shutdown_before_processing_releases_without_reading_or_posting()
    {
        var f = new WorkerFakes(); using var stop = new CancellationTokenSource(); stop.Cancel();
        await f.Processor().ExecuteAsync(f.Job, stop.Token); Assert.Equal((0, (string?)null), f.Released); Assert.DoesNotContain("position", f.Calls);
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Independent_POST_timeout_or_network_error_becomes_unknown_before_retry(bool timeout)
    {
        var f = new WorkerFakes { SubmitHandler = ct => throw (timeout ? (Exception)new OperationCanceledException() : new HttpRequestException("PRIVATE_TOKEN")) };
        await f.Processor().ExecuteAsync(f.Job, default);
        AssertOrder(f.Calls, "post:bl-v1", "close-Unknown", "retry"); AssertPrivate(f);
    }
    
    [TestCase(false)] [TestCase(true)]
    public async Task Stop_after_POST_rejection_or_no_answer_records_call_then_releases_without_attempt(bool rejected)
    {
        var f = new WorkerFakes(); using var stop = new CancellationTokenSource();
        f.SubmitHandler = ct => { stop.Cancel(); return Task.FromResult<SubmitResult>(rejected ? new SubmitResult.Rejected(503, "code", null, null) : new SubmitResult.NoAnswer()); };
        await f.Processor().ExecuteAsync(f.Job, stop.Token);
        AssertOrder(f.Calls, rejected ? "close-Failed" : "close-Unknown", "release");
        Assert.Equal((0, (string?)null), f.Released); Assert.Null(f.RetrySeconds);
    }
    [Test]
    public async Task Shutdown_after_ledger_releases_Map_and_keeps_raw_result_for_next_worker()
    {
        var f = new WorkerFakes(); using var stop = new CancellationTokenSource(); f.AfterLedger = stop.Cancel;
        await f.Processor().ExecuteAsync(f.Job, stop.Token);
        Assert.Equal("Map", f.Position.Stage); Assert.Equal((0, (string?)null), f.Released);
        Assert.DoesNotContain("parse", f.Calls); Assert.DoesNotContain("raw-delete", f.Calls);
    }
    [Test]
    public async Task Classifier_resume_polls_without_another_classification_POST_then_extracts()
    {
        var f = new WorkerFakes(); f.Accepted("classify"); f.Position = f.Position with { Stage = "Classify" };
        await f.Processor().ExecuteAsync(f.Job, default);
        Assert.NotNull(f.Classification); Assert.NotNull(f.Completion);
        Assert.Single(f.Calls, c => c.StartsWith("post:")); Assert.Contains("post:line-model", f.Calls);
    }
    
    [TestCase("Classify", null, "classifier")] [TestCase("Extract", null, "bl-v1")] [TestCase("Extract", "LINE", "line-model")]
    public async Task Submit_uses_the_active_model_for_stage_and_requested_line(string stage, string? line, string model)
    {
        var f = new WorkerFakes { Submit = new SubmitResult.Rejected(400, null, null, null) };
        f.Job = f.Job with { Stage = stage, ShippingLineCode = line }; f.Position = f.Position with { Stage = stage };
        await f.Processor().ExecuteAsync(f.Job, default); Assert.Contains("post:" + model, f.Calls);
    }
    [Test]
    public async Task Classifier_at_threshold_is_accepted_and_continues_extraction()
    {
        var f = new WorkerFakes { Class = new("line-class", .9) }; f.Position = f.Position with { Stage = "Classify" };
        await f.Processor().ExecuteAsync(f.Job, default); Assert.Equal("LINE", f.Classification!.ResolvedLine); Assert.NotNull(f.Completion);
    }
    private static void AssertOrder(List<string> calls, params string[] expected)
    {
        var previous = -1;
        foreach (var name in expected) { var index = calls.FindIndex(previous + 1, c => c == name); Assert.True(index > previous, $"Expected {name} after {previous}: {string.Join(',', calls)}"); previous = index; }
    }
    private static void AssertEnvelope(string json, Guid id, string status, string? error)
    {
        using var doc = JsonDocument.Parse(json); var e = doc.RootElement;
        Assert.Equal(id, e.GetProperty("eventId").GetGuid()); Assert.Equal(status, e.GetProperty("status").GetString());
        Assert.Equal(status == "Completed" ? "job.completed" : "job.failed", e.GetProperty("eventType").GetString());
        Assert.Equal("request-1", e.GetProperty("clientRequestId").GetString()); Assert.Equal(1, e.GetProperty("runNo").GetInt32());
        Assert.Equal(JsonValueKind.Null, e.GetProperty("result").ValueKind);
        if (error is null) Assert.Equal(JsonValueKind.Null, e.GetProperty("error").ValueKind);
        else { Assert.Equal(error, e.GetProperty("error").GetProperty("code").GetString()); Assert.True(e.GetProperty("error").GetProperty("message").GetString()!.Length <= 400); }
    }
    private static void AssertPrivate(WorkerFakes f)
    {
        foreach (var line in f.Logger.Lines)
            foreach (var secret in new[] { "PRIVATE_FIELD_VALUE", "PRIVATE_FILE.pdf", "PRIVATE_FILE_CONTENT", "PRIVATE_TOKEN" }) Assert.DoesNotContain(secret, line.Text);
    }
}
