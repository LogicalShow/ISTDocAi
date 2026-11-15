using ISTDocAi.Application.Services.DocAi.Intake;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ErrorCodes = ISTDocAi.Domain.Common.Constants.DocAi.ErrorCodes;
using ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;
using ISTDocAi.Application.Services.DocAi.ResumePolicyService;
using ISTDocAi.Application.Services.DocAi.RetryPolicyService;
using ISTDocAi.Application.Services.DocAi.ErrorClassifierService;
using ISTDocAi.Application.Services.DocAi.ClassifierPolicyService;
namespace ISTDocAi.Application.Services.DocAi.Processing;
public sealed class ProcessJob(IJobRepository jobs, IJobQueue queue, IOperationStore operations,
    ICatalogReader catalog, IModelThresholdReader thresholds, IDocumentStore documents,
    IDocumentAnalyzer analyzer, IRawResultStore raw, IAnalysisParser parser,
    IWorkerCircuitBreaker breaker, TimeProvider clock, ILogger<ProcessJob> logger, ITakeoverPolicyService takeoverPolicy, IResumePolicyService resumePolicy, IRetryPolicyService retryPolicy, IErrorClassifierService errorClassifier, IClassifierPolicyService classifierPolicy)
{
    private sealed class LeaseLostException : Exception;
    private sealed class RetryableProcessingException(string code) : Exception { public string Code { get; } = code; }

    public async Task ExecuteAsync(ClaimedJob job, CancellationToken stoppingToken)
    {
        using var ownershipLost = new CancellationTokenSource();
        using var heartbeatStop = new CancellationTokenSource();
        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, ownershipLost.Token);
        stoppingToken = work.Token;
        var heartbeat = Heartbeat();
        var started = clock.GetTimestamp();
        long? currentOperationId = null;
        using var scope = logger.BeginScope(new Dictionary<string, object?> {
            ["correlationId"] = job.CorrelationId, ["jobId"] = job.JobId, ["runNo"] = job.RunNo,
            ["consumerId"] = job.ConsumerId, ["attempt"] = job.Attempts + 1 });
        Log(LogLevel.Information, "job.claimed");
        if (job.TakeoverCount > 0) Log(LogLevel.Warning, AlertEvents.JobTakenOver);
        try
        {
            if (takeoverPolicy.IsExhausted(job.TakeoverCount))
            {
                await Fail(ErrorCodes.WorkerCrashLoop);
                return;
            }
            while (true)
            {
                stoppingToken.ThrowIfCancellationRequested();
                var position = await jobs.GetPositionAsync(job.JobId, stoppingToken)
                    ?? throw new LeaseLostException();
                if (position.RunNo != job.RunNo) throw new LeaseLostException();
                job = job with { ResolvedDocumentTypeCode = position.ResolvedDocumentTypeCode,
                    ShippingLineCode = position.ShippingLineCode, Stage = position.Stage };
                currentOperationId = position.OperationId ?? position.MapOperationId;
                switch (resumePolicy.Decide(job.TakeoverCount, position.State, position.Stage, position.UnknownInRun))
                {
                    case ResumeAction.FailCrashLoop: await Fail(ErrorCodes.WorkerCrashLoop); return;
                    case ResumeAction.FailRetriesExhausted: await Fail(ErrorCodes.RetriesExhausted); return;
                    case ResumeAction.CloseUnknown:
                        // A zero row result is ambiguous only here: a concurrent S4b may have saved the 202.
                        if (!await CloseUnknown(position.OperationId!.Value)) continue;
                        return;
                    case ResumeAction.Map:
                        using (logger.BeginScope(new Dictionary<string, object?> { ["operationId"] = position.MapOperationId }))
                            await Map(position.MapRawResultPath!, position.MapModelId!);
                        return;
                    case ResumeAction.Poll:
                        if (await Poll(position.OperationId!.Value, position.Kind!, position.ModelId!, position.OperationLocation!)) continue;
                        return;
                    default:
                        (long Id, string Kind, string Model, string Location)? next;
                        try { next = await Submit(position.Stage); }
                        catch (RereadPositionException) { continue; }
                        if (next is null) return;
                        if (await Poll(next.Value.Id, next.Value.Kind, next.Value.Model, next.Value.Location)) continue;
                        return;
                }
            }
        }
        catch (LeaseLostException) { Log(LogLevel.Warning, "job.lease_lost"); }
        catch (Exception) when (ownershipLost.IsCancellationRequested) { Log(LogLevel.Warning, "job.lease_lost"); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await GuardRecovery(() => Release(TimeSpan.Zero, null));
        }
        catch (DocumentMissingException)
        {
            await GuardRecovery(() => stoppingToken.IsCancellationRequested ? Release(TimeSpan.Zero, null) : Fail(ErrorCodes.OriginalMissing));
        }
        catch (RetryableProcessingException error)
        {
            await GuardRecovery(() => stoppingToken.IsCancellationRequested ? Release(TimeSpan.Zero, null) : Retry(code: error.Code));
        }
        catch (Exception)
        {
            // Exception messages can contain document content, URLs, credentials or SQL parameter values.
            Log(LogLevel.Error, "job.processing_error", ErrorCodes.InternalError);
            await GuardRecovery(() => stoppingToken.IsCancellationRequested ? Release(TimeSpan.Zero, null) : Retry());
        }

        finally
        {
            await heartbeatStop.CancelAsync();
            await heartbeat;
        }

        void EnsureOwned() => ownershipLost.Token.ThrowIfCancellationRequested();
        async Task Heartbeat()
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
                while (await timer.WaitForNextTickAsync(heartbeatStop.Token))
                {
                    using var budget = CancellationTokenSource.CreateLinkedTokenSource(heartbeatStop.Token);
                    budget.CancelAfter(TimeSpan.FromSeconds(10));
                    if (await queue.RenewAsync(new(job.JobId, job.LeaseToken), budget.Token) != WriteOutcome.Applied)
                    {
                        await ownershipLost.CancelAsync();
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (heartbeatStop.IsCancellationRequested) { }
            catch (Exception)
            {
                // An unconfirmed renewal is unsafe: abandon ownership without any recovery writes.
                logger.LogError("{event}", "job.lease_renewal_failed");
                await ownershipLost.CancelAsync();
            }
        }
        async Task GuardRecovery(Func<Task> action)
        {
            try { EnsureOwned(); await action(); }
            catch (LeaseLostException) { Log(LogLevel.Warning, "job.lease_lost"); }
            catch (Exception) { Log(LogLevel.Error, "job.recovery_failed", ErrorCodes.InternalError); }
        }
        void Log(LogLevel level, string name, string? code = null) =>
            logger.Log(level, "{event} errorCode={errorCode} operationId={operationId} durationMs={durationMs}", name, code, currentOperationId,
                clock.GetElapsedTime(started).TotalMilliseconds);
        void Check(WriteOutcome outcome)
        {
            if (outcome != WriteOutcome.Applied) throw new LeaseLostException();
        }
        Lease Lease() => new(job.JobId, job.LeaseToken);
        // SQL has its own command timeout. Its writes must survive host cancellation, particularly S4b.
        async Task<WriteOutcome> Write(Func<CancellationToken, Task<WriteOutcome>> write)
        {
            EnsureOwned();
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ownershipLost.Token);
            budget.CancelAfter(TimeSpan.FromSeconds(30));
            return await write(budget.Token);
        }
        async Task<(Guid Id, string Json)> Event(string? code, string? resultType = null)
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var run = await jobs.GetRunAsync(job.JobId, job.ConsumerId, job.RunNo, budget.Token)
                ?? throw new LeaseLostException();
            var id = Guid.CreateVersion7();
            var envelope = new WebhookEnvelope { EventId = id, EventType = code is null ? AlertEvents.JobCompleted : AlertEvents.JobFailed,
                RunReason = run.RunReason, Changes = RunChangeJson.Read(run.ChangesJson),
                JobId = job.JobId, RunNo = job.RunNo, ClientRequestId = run.ClientRequestId ?? "",
                Status = code is null ? "Completed" : "Failed", Error = code is null ? null : new(code, Message(code)),
                Result = null, OccurredAtUtc = clock.GetUtcNow(),
                RequestedDocumentType = run.RequestedDocumentTypeCode,
                DocumentType = run.ResolvedDocumentTypeCode,
                ResultType = code is null ? resultType : null };
            return (id, JsonSerializer.Serialize(envelope, ContractJson.Options));
        }
        async Task Fail(string code, bool counts = false, long? operationId = null, string? azureCode = null)
        {
            var e = await Event(code);
            Check(await Write(ct => jobs.FailAsync(new(job.JobId, job.LeaseToken, code, Message(code), counts,
                e.Id, e.Json, operationId, Cut(azureCode)), ct)));
            Log(code is ErrorCodes.InvalidDocument or ErrorCodes.Unclassified ? LogLevel.Information : LogLevel.Error, AlertEvents.JobFailed, code);
        }
        async Task Retry(TimeSpan? retryAfter = null, string code = ErrorCodes.InternalError)
        {
            var decision = retryPolicy.Failure(job.Attempts, retryAfter).Value
                ?? throw new InvalidOperationException("Invalid retry state.");
            if (decision.Action == RetryAction.Fail) { await Fail(decision.ErrorCode!, true); return; }
            Check(await Write(ct => queue.RetryAsync(Lease(), Seconds(decision.Delay), code, ct)));
            Log(LogLevel.Warning, "job.retry", code);
        }
        async Task Release(TimeSpan delay, string? code)
        {
            Check(await Write(ct => queue.ReleaseAsync(Lease(), Seconds(delay), code, ct)));
            Log(LogLevel.Information, "job.released", code);
        }
        async Task Auth()
        {
            breaker.Open();
            var decision = retryPolicy.Configuration(job.Attempts);
            await Release(decision.Delay, decision.ErrorCode);
        }
        async Task<bool> CloseUnknown(long id)
        {
            using var operationScope = logger.BeginScope(new Dictionary<string, object> { ["operationId"] = id });
            var outcome = await Write(ct => operations.CloseSendingAsync(id, job.LeaseToken, "Unknown", null, ct));
            if (outcome == WriteOutcome.LeaseLost)
            {
                // S4c cannot distinguish a stale lease from a saved 202. Fence before re-reading S19.
                Check(await Write(ct => queue.RenewAsync(Lease(), ct)));
                return false;
            }
            Check(outcome);
            Log(LogLevel.Warning, AlertEvents.OperationUnknown);
            stoppingToken.ThrowIfCancellationRequested();
            await Retry(code: ErrorCodes.AzureUnavailable);
            return true;
        }
        async Task<(long Id, string Kind, string Model, string Location)?> Submit(string stage)
        {
            var kind = stage == "Classify" ? "classify" : "extract";
            var models = await catalog.ReadAsync(stoppingToken);
            var type = kind == "classify" ? job.DocumentTypeCode : job.ResolvedDocumentTypeCode;
            var model = models.SingleOrDefault(m => m.DocumentTypeCode == type && m.Kind == kind &&
                m.ShippingLineCode == (kind == "classify" ? null : job.ShippingLineCode));
            if (model is null) { await Fail(ErrorCodes.ModelNotFound); return null; }
            using var bytes = new MemoryStream();
            await Storage(async () =>
            {
                await using var original = await documents.OpenReadAsync(job.BlobPath, stoppingToken);
                await original.CopyToAsync(bytes, stoppingToken);
                return true;
            });
            SubmitPermit permit;
            try { permit = await analyzer.ReserveSubmitAsync(stoppingToken); }
            catch (AnalysisUnavailableException e)
            {
                stoppingToken.ThrowIfCancellationRequested();
                if (e.Reason == AnalysisUnavailableReason.RateLimited) await Release(TimeSpan.FromSeconds(10), null);
                else await Auth();
                return null;
            }
            stoppingToken.ThrowIfCancellationRequested();
            EnsureOwned();
            using var beginBudget = CancellationTokenSource.CreateLinkedTokenSource(ownershipLost.Token);
            beginBudget.CancelAfter(TimeSpan.FromSeconds(30));
            var begin = await operations.BeginAsync(Lease(), kind, model.ModelId, beginBudget.Token);
            if (begin.Outcome == WriteOutcome.CancelRequested)
            {
                Check(await Write(ct => jobs.FinishCancellationAsync(Lease(), ct)));
                return null;
            }
            if (begin.Outcome == WriteOutcome.RetriesExhausted) { await Fail(ErrorCodes.RetriesExhausted); return null; }
            Check(begin.Outcome);
            var id = begin.OperationId ?? throw new LeaseLostException();
            currentOperationId = id;
            using var operationScope = logger.BeginScope(new Dictionary<string, object> { ["operationId"] = id });
            Log(LogLevel.Information, "operation.sending");
            // Once Sending is recorded, finish the POST and its fact write on independent budgets.
            EnsureOwned();
            using var postBudget = CancellationTokenSource.CreateLinkedTokenSource(ownershipLost.Token);
            postBudget.CancelAfter(TimeSpan.FromSeconds(100));
            SubmitResult response;
            try
            {
                response = await analyzer.SubmitAsync(permit, kind == "classify" ? OperationKind.Classify : OperationKind.Extract,
                    model.ModelId, bytes.GetBuffer().AsMemory(0, checked((int)bytes.Length)), postBudget.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or HttpRequestException)
            {
                response = new SubmitResult.NoAnswer();
            }
            EnsureOwned();
            if (response is SubmitResult.Accepted accepted)
            {
                using var acceptedBudget = CancellationTokenSource.CreateLinkedTokenSource(ownershipLost.Token);
                acceptedBudget.CancelAfter(TimeSpan.FromSeconds(30));
                if (!await operations.AcceptedAsync(id, accepted.OperationLocation, acceptedBudget.Token)) throw new LeaseLostException();
                Log(LogLevel.Information, "operation.accepted");
                // Host cancellation is observed only after the location is durable.
                stoppingToken.ThrowIfCancellationRequested();
                if (accepted.RetryAfter is { } delay && !await Wait(delay)) return null;
                return (id, kind, model.ModelId, accepted.OperationLocation);
            }
            if (response is SubmitResult.Rejected rejected)
            {
                Check(await Write(ct => operations.CloseSendingAsync(id, job.LeaseToken, "Failed", Cut(rejected.Code), ct)));
                stoppingToken.ThrowIfCancellationRequested();
                await Http(ErrorSource.PostAnalyze, rejected.Status, rejected.RetryAfter);
            }
            else if (!await CloseUnknown(id))
            {
                // The 202 won the race; the outer resume loop reads its persisted location.
                throw new RereadPositionException();
            }
            return null;
        }
        async Task Http(ErrorSource source, int status, TimeSpan? retryAfter, long? id = null)
        {
            var classified = errorClassifier.Classify(source, status);
            if (classified.Kind == ErrorKind.Configuration) await Auth();
            else if (classified.Kind == ErrorKind.Retryable) await Retry(retryAfter, status == 429 ? ErrorCodes.AzureThrottled : ErrorCodes.AzureUnavailable);
            else
            {
                if (status == 404) Log(LogLevel.Error, source == ErrorSource.PostAnalyze ? AlertEvents.RegistryDrift : AlertEvents.AnalysisResultMissing, classified.Error.Code);
                await Fail(classified.Error.Code, operationId: id);
            }
        }
        async Task<bool> Wait(TimeSpan delay)
        {
            var decision = retryPolicy.PollWait(job.Attempts, delay).Value ?? throw new InvalidOperationException("Invalid poll delay.");
            if (decision.Action == RetryAction.Release) { await Release(decision.Delay, null); return false; }
            await Task.Delay(decision.Delay, clock, stoppingToken);
            return true;
        }
        async Task<bool> Poll(long id, string kind, string modelId, string location)
        {
            using var operationScope = logger.BeginScope(new Dictionary<string, object> { ["operationId"] = id });
            while (true)
            {
                stoppingToken.ThrowIfCancellationRequested();
                PollResult answer;
                try { answer = await analyzer.PollAsync(location, stoppingToken); }
                catch (AnalysisUnavailableException e)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    if (e.Reason == AnalysisUnavailableReason.CredentialUnavailable) { await Auth(); return false; }
                    await Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken);
                    continue;
                }
                stoppingToken.ThrowIfCancellationRequested();
                switch (answer)
                {
                    case PollResult.Running running:
                        if (!await Wait(running.RetryAfter)) return false;
                        break;
                    case PollResult.HttpError error:
                        await Http(ErrorSource.GetPoll, error.Status, error.RetryAfter, id); return false;
                    case PollResult.NoAnswer: await Retry(code: ErrorCodes.AzureUnavailable); return false;
                    case PollResult.NotFound:
                        Log(LogLevel.Error, AlertEvents.AnalysisResultMissing, ErrorCodes.AnalysisResultMissing);
                        await Fail(ErrorCodes.AnalysisResultMissing, operationId: id); return false;
                    case PollResult.Failed failed: await Fail(ErrorCodes.AnalysisFailed, operationId: id, azureCode: failed.Code); return false;
                    case PollResult.Succeeded succeeded:
                        if (kind == "classify")
                        {
                            var parsed = Mapping(() => parser.ParseClassifier(succeeded.RawJson));
                            var mapping = parsed.Name is null ? null : await jobs.GetClassMappingAsync(modelId, parsed.Name, stoppingToken);
                            var route = classifierPolicy.ResolveRoute(job.DocumentTypeCode, mapping?.TargetDocumentTypeCode,
                                mapping?.ShippingLineCode, parsed.Confidence, (double)(mapping?.MinClassConfidence ?? 1));
                            var e = await Event(ErrorCodes.Unclassified);
                            Check(await Write(ct => jobs.ClassifierSucceededAsync(new(job.JobId, job.LeaseToken, id, modelId,
                                succeeded.Pages, succeeded.CompletedAtUtc, parsed.Name,
                                double.IsFinite(parsed.Confidence) && parsed.Confidence is >= 0 and <= 1 ? (decimal)parsed.Confidence : null,
                                route?.ShippingLineCode, route is null ? Message(ErrorCodes.Unclassified) : null, e.Id, e.Json,
                                route?.DocumentTypeCode), ct)));
                            if (route is null) { Log(LogLevel.Information, AlertEvents.JobFailed, ErrorCodes.Unclassified); return false; }
                            // The next loop always rereads S19, including after a crash immediately after S18.
                            return true;
                        }
                        var path = await Storage(() => raw.SaveAsync(job.JobId, job.RunNo, id, succeeded.RawJson, stoppingToken));
                        Check(await Write(ct => operations.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, id, modelId,
                            succeeded.Pages, succeeded.CompletedAtUtc, path), ct)));
                        await BestEffort(ct => analyzer.DeleteResultAsync(location, ct), "analysis.deleted");
                        stoppingToken.ThrowIfCancellationRequested();
                        await Map(path, modelId);
                        return false;
                }
            }
        }
        async Task Map(string path, string modelId)
        {
            var json = await Storage(() => raw.ReadAsync(path, stoppingToken));
            var threshold = await thresholds.ReadThresholdAsync(modelId, stoppingToken)
                ?? throw new InvalidOperationException("Producing model threshold missing.");
            var mapped = Mapping(() => parser.Map(json, job.ResolvedDocumentTypeCode
                ?? throw new InvalidOperationException("Extraction route is unresolved."), job.ShippingLineCode, modelId, (double)threshold));
            var resultJson = Mapping(() => CanonicalResultCodec.Serialize(mapped.Result));
            var e = await Event(null, mapped.ResultType);
            Check(await Write(ct => jobs.CompleteAsync(new(job.JobId, job.LeaseToken,
                resultJson, checked((short)mapped.FlaggedFields), e.Id, e.Json, mapped.ResultType, mapped.MappingFingerprint), ct)));
            Log(LogLevel.Information, AlertEvents.JobCompleted);
            await BestEffort(ct => raw.DeleteAsync(path, ct), "raw.deleted");
        }
        async Task<T> Storage<T>(Func<Task<T>> action)
        {
            try { EnsureOwned(); return await action(); }
            catch (DocumentMissingException) { throw; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception) { throw new RetryableProcessingException(ErrorCodes.StorageUnavailable); }
        }
        T Mapping<T>(Func<T> action)
        {
            try { EnsureOwned(); return action(); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception) { throw new RetryableProcessingException(ErrorCodes.MappingFailed); }
        }
        async Task BestEffort(Func<CancellationToken, Task> action, string name)
        {
            EnsureOwned();
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ownershipLost.Token);
            budget.CancelAfter(TimeSpan.FromSeconds(5));
            try { await action(budget.Token); Log(LogLevel.Information, name); }
            catch (Exception) { Log(LogLevel.Warning, name + ".failed"); }
        }
    }
    private sealed class RereadPositionException : Exception;
    private static int Seconds(TimeSpan delay) => checked((int)Math.Ceiling(delay.TotalSeconds));
    private static string? Cut(string? value) => value is { Length: > 50 } ? value[..50] : value;
    private static string Message(string code) => code switch
    {
        ErrorCodes.WorkerCrashLoop => "Processing stopped after three worker takeovers.",
        ErrorCodes.RetriesExhausted => "The processing retry budget was exhausted.",
        ErrorCodes.ModelNotFound => "No analysis model is available for this document.",
        ErrorCodes.OriginalMissing => "The original document is no longer available.",
        ErrorCodes.InvalidDocument => "The analysis service could not accept this document.",
        ErrorCodes.Unclassified => "The document class is unmapped or below its confidence threshold.",
        ErrorCodes.AnalysisFailed => "The document analysis failed.",
        ErrorCodes.AnalysisResultMissing => "The saved analysis result is no longer available.",
        _ => "Document processing encountered a temporary error."
    };
}
