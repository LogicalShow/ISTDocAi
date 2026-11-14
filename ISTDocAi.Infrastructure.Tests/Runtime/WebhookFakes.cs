using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.Webhooks;
using ISTDocAi.Application.Services.DocAi.WebhookPolicyService;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Constants.DocAi;


using Microsoft.Extensions.Logging;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class WebhookFakes : IWebhookOutbox, ICallbackDirectory, ISecretStore, IWebhookTransport
{
    public TestClock Clock { get; } = new();
    public TestLogger<DeliverWebhook> Logs { get; } = new();
    public TestLogger<SecretFailureLog> SecretLogs { get; } = new();
    public SecretFailureLog FailureLog { get; }
    public ClaimedDelivery Delivery { get; set; } = new(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1,
        "job.completed", "{\"eventType\":\"job.completed\",\"error\":null,\"result\":null,\"last\":1}", 0);
    public EventContext Context { get; set; } = new("test-correlation", "{\"privateField\":\"PRIVATE_VALUE\"}");
    public CallbackConsumer? Callback { get; set; } = new(1, "TEST", "https://private.example/callback", "secret", null);
    public byte[] Key { get; } = Convert.FromBase64String("ZG9jYWktdGVzdC12ZWN0b3Itc2VjcmV0LTMyYnl0ZXM=");
    public List<WebhookRequest> Requests { get; } = [];
    public List<(short? Status, string? Error)> Attempts { get; } = [];
    public WebhookResponse Response { get; set; } = new(200);
    public WriteOutcome Write { get; set; } = WriteOutcome.Applied;
    public bool SecretFailure { get; set; }
    public int Invalidations { get; private set; }
    public int Claims;
    public int SecretReads;
    public Func<CancellationToken, Task<IReadOnlyList<ClaimedDelivery>>>? Claim { get; set; }
    public Func<WebhookRequest, CancellationToken, Task<WebhookResponse>>? Send { get; set; }
    public Func<CancellationToken, Task<WriteOutcome>>? Record { get; set; }
    public bool RecordWasCancelled { get; private set; }
    public WebhookFakes() => FailureLog = new(Clock, SecretLogs);
    public DeliverWebhook Processor(IWebhookOutbox? outbox = null, IWebhookTransport? transport = null) =>
        new(outbox ?? this, this, this, transport ?? this, Clock, FailureLog, Logs, new ISTDocAi.Application.Services.DocAi.WebhookPolicyService.WebhookPolicyService(), new DocAiV2Json());
    public Task<IReadOnlyList<ClaimedDelivery>> ClaimDueAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Claims);
        return Claim?.Invoke(ct) ?? Task.FromResult<IReadOnlyList<ClaimedDelivery>>([]);
    }
    public Task<EventContext?> GetEventContextAsync(Guid jobId, short runNo, CancellationToken ct) => Task.FromResult<EventContext?>(Context);
    public Task<WriteOutcome> RecordAttemptAsync(long id, Guid token, short? status, string? error, CancellationToken ct)
    {
        RecordWasCancelled |= ct.IsCancellationRequested;
        lock (Attempts) Attempts.Add((status, error));
        return Record?.Invoke(ct) ?? Task.FromResult(Write);
    }
    public Task<int> ReplayAsync(short consumerId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<CallbackConsumer>> ReadAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CallbackConsumer>>(Callback is null ? [] : [Callback]);
    public Task<byte[]> GetSigningKeyAsync(string name, CancellationToken ct)
    {
        SecretReads++;
        if (SecretFailure) throw new SecretUnavailableException(name);
        return Task.FromResult(Key);
    }
    public Task<string> GetCaCertificatePemAsync(string name, CancellationToken ct) => throw new SecretUnavailableException(name);
    public void InvalidateSigningKey(string name) => Invalidations++;
    public Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken ct)
    {
        lock (Requests) Requests.Add(request);
        return Send?.Invoke(request, ct) ?? Task.FromResult(Response);
    }
}
