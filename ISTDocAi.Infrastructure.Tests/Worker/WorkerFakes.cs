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
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;

using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Application.Services.DocAi.TakeoverPolicyService;
using ISTDocAi.Application.Services.DocAi.ResumePolicyService;
using ISTDocAi.Application.Services.DocAi.RetryPolicyService;
using ISTDocAi.Application.Services.DocAi.ErrorClassifierService;
using ISTDocAi.Application.Services.DocAi.ClassifierPolicyService;

using ISTDocAi.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;

public sealed class TestClock : TimeProvider
{
    private readonly object gate = new();
    private DateTimeOffset now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly List<Timer> timers = [];
    public List<TimeSpan> Delays { get; } = [];
    public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
    public override long GetTimestamp() => GetUtcNow().Ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            Delays.Add(dueTime);
            var timer = new Timer(this, callback, state);
            timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
    }
    public void Advance(TimeSpan duration)
    {
        List<Timer> due;
        lock (gate)
        {
            now += duration;
            due = timers.Where(t => !t.Disposed && t.Due <= now).ToList();
            foreach (var t in due) t.Due = t.Period > TimeSpan.Zero ? now + t.Period : DateTimeOffset.MaxValue;
        }
        foreach (var t in due) if (!t.Disposed) t.Callback(t.State);
    }
    private sealed class Timer(TestClock owner, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public DateTimeOffset Due { get; set; }
        public TimeSpan Period { get; set; }
        public bool Disposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate)
            {
                if (Disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : owner.now + dueTime;
                Period = period;
                return true;
            }
        }
        public void Dispose() { lock (owner.gate) Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

public sealed class TestLogger<T> : ILogger<T>
{
    public ConcurrentQueue<(LogLevel Level, string Text)> Lines { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Lines.Enqueue((logLevel, formatter(state, exception)));
}

public sealed class WorkerFakes : IJobRepository, IJobQueue, IOperationStore, ICatalogReader, IModelThresholdReader,
    IDocumentStore, IDocumentAnalyzer, IRawResultStore, IAnalysisParser, IWorkerCircuitBreaker
{
    public const string RawJson = """{"pages":[{}],"documents":[{"docType":"line-class","confidence":0.95,"fields":{"shipperName":{"valueString":"PRIVATE_FIELD_VALUE","confidence":0.85}}}]}""";
    public ClaimedJob Job { get; set; } = new(Guid.NewGuid(), 1, Guid.NewGuid(), 0, 1, "BL", null, "Extract", "PRIVATE_FILE.pdf", 1, 0, "test-correlation");
    public RunPosition Position { get; set; } = new(1, "Extract", null, null, null, null, null, null, 0, null, null);
    public bool CancelRequested { get; set; }
    public List<string> Calls { get; } = [];
    public Dictionary<string, WriteOutcome> Outcomes { get; } = [];
    public List<CatalogModel> Models { get; } = [new("BL", null, "extract", "bl-v1", .9m), new("BL", null, "classify", "classifier", .9m), new("BL", "LINE", "extract", "line-model", .9m)];
    public Queue<object> Polls { get; } = new();
    public object Reserve { get; set; } = new Permit();
    public SubmitResult Submit { get; set; } = new SubmitResult.Accepted("https://test/operations/1", null);
    public Func<CancellationToken, Task<SubmitResult>>? SubmitHandler { get; set; }
    public Action? AfterClose { get; set; }
    public CancellationToken AcceptedToken { get; private set; }
    public string? ClosedAzureCode { get; private set; }
    public Action? AfterAccepted { get; set; }
    public Func<CancellationToken, Task<bool>>? AcceptedHandler { get; set; }
    public Func<CancellationToken, Task<WriteOutcome>>? RenewHandler { get; set; }
    public Action? AfterLedger { get; set; }
    public Action? AfterClassification { get; set; }
    public IAnalysisParser? MappingParser { get; set; }
    public List<string> MeterEntries { get; } = [];
    public Action? OnParse { get; set; }
    public bool Missing { get; set; }
    public bool StorageError { get; set; }
    public bool ParseError { get; set; }
    public bool DeleteError { get; set; }
    public bool MapRejected { get; set; }
    public decimal Threshold { get; set; } = .9m;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? StorageFailureAt { get; set; }
    public MemoryStream? OriginalDestination { get; private set; }
    public ReadOnlyMemory<byte> SubmittedBytes { get; private set; }
    public string? RetryError { get; private set; }
    public string Raw { get; set; } = RawJson;
    public ClassMapping? Mapping { get; set; } = new("LINE", .9m) { TargetDocumentTypeCode = "BL" };
    public ParsedClass Class { get; set; } = new("line-class", .95);
    public Failure? Failure { get; private set; }
    public Completion? Completion { get; private set; }
    public ClassifierSuccess? Classification { get; private set; }
    public int? RetrySeconds { get; private set; }
    public (int Delay, string? Error)? Released { get; private set; }
    public int OpenCount { get; private set; }
    public TimeSpan Remaining { get; set; }
    public TestClock Clock { get; } = new();
    public TestLogger<ProcessJob> Logger { get; } = new();
    public ProcessJob Processor(IJobRepository? jobs = null, IJobQueue? queue = null, IOperationStore? operations = null,
        ICatalogReader? catalog = null, IModelThresholdReader? thresholds = null) => new(jobs ?? this, queue ?? this, operations ?? this,
            catalog ?? this, thresholds ?? this, this, this, this, this, this, Clock, Logger, Rules.Get<ITakeoverPolicyService>(), Rules.Get<IResumePolicyService>(), Rules.Get<IRetryPolicyService>(), Rules.Get<IErrorClassifierService>(), Rules.Get<IClassifierPolicyService>());
    private WriteOutcome Write(string name)
    {
        Calls.Add(name);
        return Outcomes.GetValueOrDefault(name, WriteOutcome.Applied);
    }
    public void Accepted(string kind = "extract") => Position = Position with { OperationId = 1, Kind = kind, ModelId = "bl-v1", State = "Accepted", OperationLocation = "https://test/operations/1" };
    public Task<RunPosition?> GetPositionAsync(Guid id, CancellationToken ct)
    {
        Calls.Add("position");
        return Task.FromResult<RunPosition?>(Position with {
            RequestedDocumentTypeCode = Position.RequestedDocumentTypeCode ?? Job.DocumentTypeCode,
            ResolvedDocumentTypeCode = Position.ResolvedDocumentTypeCode ?? (Job.DocumentTypeCode == "AUTO" ? null : Job.DocumentTypeCode),
            ShippingLineCode = Position.RequestedDocumentTypeCode is null ? Job.ShippingLineCode : Position.ShippingLineCode });
    }
    public string RunReason { get; set; } = "New";
    public string? ChangesJson { get; set; }
    public Task<RunOutcome?> GetRunAsync(Guid id, short consumer, short? run, CancellationToken ct) =>
        Task.FromResult<RunOutcome?>(new(id, run ?? 1, "request-1", "Processing", null, null, Clock.GetUtcNow(), null, Job.PageCount, null, null, null) {
            RunReason = RunReason, ChangesJson = ChangesJson,
            RequestedDocumentTypeCode = Job.DocumentTypeCode,
            ResolvedDocumentTypeCode = Position.ResolvedDocumentTypeCode ?? (Job.DocumentTypeCode == "AUTO" ? null : Job.DocumentTypeCode) });
    public Task<WriteOutcome> CompleteAsync(Completion request, CancellationToken ct) { Completion = request; return Task.FromResult(Write("complete")); }
    public Task<CancelJobResult?> CancelAsync(short consumerId, string requestId, CancellationToken ct) => Task.FromResult<CancelJobResult?>(null);
    public Task<WriteOutcome> FinishCancellationAsync(Lease lease, CancellationToken ct)
    {
        Failure = new(lease.JobId, lease.LeaseToken, "CANCELLED", "Cancelled by the consumer.", false, Guid.Empty, "", null, null);
        return Task.FromResult(Write("cancel"));
    }
    public Task<WriteOutcome> FailAsync(Failure request, CancellationToken ct) { Failure = request; return Task.FromResult(Write("fail")); }
    public Task<WriteOutcome> ClassifierSucceededAsync(ClassifierSuccess request, CancellationToken ct)
    {
        Classification = request;
        var outcome = Write("classify");
        if (outcome == WriteOutcome.Applied)
        {
            Position = new(1, request.ResolvedDocumentTypeCode is null ? "Classify" : "Extract", null, null, null, null, null, null, 0, null, null) {
                RequestedDocumentTypeCode = Job.DocumentTypeCode, ResolvedDocumentTypeCode = request.ResolvedDocumentTypeCode,
                ShippingLineCode = request.ResolvedLine };
            MeterEntries.Add("classify");
            AfterClassification?.Invoke();
        }
        return Task.FromResult(outcome);
    }
    public Task<ClassMapping?> GetClassMappingAsync(string model, string name, CancellationToken ct) { Calls.Add("class-map"); return Task.FromResult(Mapping); }
    public Task<ClaimedJob?> ClaimAsync(CancellationToken ct) { Calls.Add("claim"); return Task.FromResult<ClaimedJob?>(null); }
    public Task<WriteOutcome> RenewAsync(Lease lease, CancellationToken ct) => RenewHandler?.Invoke(ct) ?? Task.FromResult(Write("renew"));
    public Task<WriteOutcome> RetryAsync(Lease lease, int delay, string? error, CancellationToken ct) { RetrySeconds = delay; RetryError = error; return Task.FromResult(Write("retry")); }
    public Task<WriteOutcome> ReleaseAsync(Lease lease, int delay, string? error, CancellationToken ct) { Released = (delay, error); return Task.FromResult(Write("release")); }
    public Task<BeginOperationResult> BeginAsync(Lease lease, string kind, string model, CancellationToken ct) { return Task.FromResult(new BeginOperationResult(CancelRequested && Classification is null && Position.State is null ? WriteOutcome.CancelRequested : Write("begin"), 2)); }
    public Task<bool> AcceptedAsync(long id, string location, CancellationToken ct) { AcceptedToken = ct; var result = Write("accepted") == WriteOutcome.Applied; AfterAccepted?.Invoke(); return AcceptedHandler?.Invoke(ct) ?? Task.FromResult(result); }
    public Task<WriteOutcome> CloseSendingAsync(long id, Guid token, string state, string? code, CancellationToken ct)
    {
        ClosedAzureCode = code;
        var outcome = Write("close-" + state);
        AfterClose?.Invoke();
        return Task.FromResult(outcome);
    }
    public Task<WriteOutcome> ExtractionSucceededAsync(ExtractionSuccess request, CancellationToken ct)
    {
        var result = Write("ledger");
        if (result == WriteOutcome.Applied)
        {
            Position = Position with { Stage = "Map", OperationId = null, State = null, Kind = null, ModelId = null,
                MapOperationId = request.OperationId, MapRawResultPath = request.RawResultPath, MapModelId = request.ModelId };
            MeterEntries.Add("extract");
        }
        AfterLedger?.Invoke();
        return Task.FromResult(result);
    }
    public Task<IReadOnlyList<CatalogModel>> ReadAsync(CancellationToken ct) { Calls.Add("catalog"); return Task.FromResult<IReadOnlyList<CatalogModel>>(Models); }
    public Task<decimal?> ReadThresholdAsync(string model, CancellationToken ct) { Calls.Add("threshold:" + model); return Task.FromResult<decimal?>(Threshold); }
    public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        Calls.Add("original");
        if (Missing) throw new DocumentMissingException();
        if (StorageError) throw new IOException("PRIVATE_TOKEN");
        return Task.FromResult<Stream>(new OriginalStream(this));
    }
    private sealed class OriginalStream(WorkerFakes owner) : MemoryStream(Encoding.UTF8.GetBytes("PRIVATE_FILE_CONTENT"))
    {
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            owner.OriginalDestination = (MemoryStream)destination;
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }
    public sealed class Permit : SubmitPermit;
    public Func<CancellationToken, Task<bool>>? AccessHandler { get; set; }
    public int AccessChecks;
    public Task<bool> CheckAccessAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref AccessChecks);
        return AccessHandler?.Invoke(ct) ?? Task.FromResult(true);
    }
    public Task<SubmitPermit> ReserveSubmitAsync(CancellationToken ct)
    {
        Calls.Add("reserve");
        if (Reserve is Exception error) throw error;
        return Task.FromResult((SubmitPermit)Reserve);
    }
    public Task<SubmitResult> SubmitAsync(SubmitPermit permit, OperationKind kind, string model, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        Calls.Add("post:" + model);
        SubmittedBytes = bytes;
        return SubmitHandler is null ? Task.FromResult(Submit) : SubmitHandler(ct);
    }
    public Task<PollResult> PollAsync(string location, CancellationToken ct)
    {
        Calls.Add("poll");
        var next = Polls.Count == 0 ? new PollResult.Succeeded(Raw, CompletedAtUtc ?? Clock.GetUtcNow(), 1) : Polls.Dequeue();
        if (next is Exception error) throw error;
        return Task.FromResult((PollResult)next);
    }
    public Task DeleteResultAsync(string location, CancellationToken ct) { Calls.Add("azure-delete"); if (DeleteError) throw new IOException("PRIVATE_TOKEN"); return Task.CompletedTask; }
    public Task<string> SaveAsync(Guid id, short run, long operation, string json, CancellationToken ct) { Calls.Add("raw-save"); if (StorageFailureAt == "save") throw new IOException("PRIVATE_TOKEN"); Raw = json; return Task.FromResult("raw-path"); }
    public Task<string> ReadAsync(string path, CancellationToken ct) { Calls.Add("raw-read"); if (StorageFailureAt == "read") throw new IOException("PRIVATE_TOKEN"); return Task.FromResult(Raw); }
    public Task DeleteAsync(string path, CancellationToken ct) { Calls.Add("raw-delete"); if (DeleteError) throw new IOException("PRIVATE_TOKEN"); return Task.CompletedTask; }
    public MappedResult Map(string json, string type, string? line, string model, double minFieldConfidence)
    {
        if (MappingParser is not null) return MappingParser.Map(json, type, line, model, minFieldConfidence);
        var mapped = Rules.Get<ISTDocAi.Application.Services.DocAi.CanonicalResultMapperService.ICanonicalResultMapperService>().Map(Parse(json, type, line, model), minFieldConfidence).Value
            ?? throw new InvalidOperationException("Invalid canonical result.");
        return new(CanonicalResultProjection.BillOfLading(mapped), mapped.FlaggedFields, ISTDocAi.Application.Common.Contracts.DocAi.V1.ResultTypes.BillOfLading);
    }
    public ExtractedDocument Parse(string json, string type, string? line, string model)
    {
        Calls.Add("parse"); OnParse?.Invoke();
        if (ParseError) throw new FormatException("PRIVATE_FIELD_VALUE");
        var parsed = AnalyzeResultParser.Parse(json, type, line, model);
        return MapRejected ? parsed with { PageCount = 0 } : parsed;
    }
    public ParsedClass ParseClassifier(string json) { Calls.Add("parse-class"); if (ParseError) throw new FormatException("PRIVATE_FIELD_VALUE"); return Class; }
    public Task<ClaimedJob?> ClaimAsync(IJobQueue queue, CancellationToken ct) => Remaining > TimeSpan.Zero ? Task.FromResult<ClaimedJob?>(null) : queue.ClaimAsync(ct);
    public void Open() { OpenCount++; Remaining = TimeSpan.FromMinutes(5); }
    public Task<int> DailyPagesAsync(short id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IntakeResult> IntakeAsync(IntakeRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task PutAsync(string path, Stream stream, string type, CancellationToken ct) => throw new NotSupportedException();
    public Task CheckAsync(CancellationToken ct) => Task.CompletedTask;
}
