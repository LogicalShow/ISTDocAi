using NUnit.Framework;
using Assert = ISTDocAi.Infrastructure.Tests.DocAi.Runtime.PortAssert;
using System.Text.Json.Nodes;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;


namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

[Category("Sql")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class WebhookSqlIntegrationTests 
{
    private readonly SqlDatabase db = new();
    [SetUp]
    public Task InitializeAsync() => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")) ? Task.CompletedTask : db.InitializeAsync();
    [TearDown]
    public Task DisposeAsync() => db.DisposeAsync();
    private async Task<ClaimedDelivery> CompletedEvent()
    {
        var job = await db.NewClaim(); var operation = await db.Accepted(job);
        await db.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 2, DateTimeOffset.UtcNow, "raw"), default);
        await db.Store.CompleteAsync(new(job.JobId, job.LeaseToken, "{\"value\":42}", 0, Guid.NewGuid(), "{\"error\":null,\"result\":null}"), default);
        return Assert.Single(await SqlClaimTestHelper.ExpectedDeliveriesAsync(db.Store.ClaimDueAsync));
    }
    [TestCase("CONNECTION_FAILED")] [TestCase("TIMEOUT")] [TestCase("TLS_FAILED")]
    public async Task Connection_retry_is_immediate_once_then_backoff_and_eighth_failure_is_final(string error)
    {
        SqlTestGuard.Require();
        var delivery = await CompletedEvent();
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            await db.Store.RecordAttemptAsync(delivery.DeliveryId, delivery.LeaseToken, null, error, default);
            var immediate = attempt < 8 && attempt % 2 == 1;
            Assert.Equal(error + (immediate ? ":IMMEDIATE" : ""), await db.Scalar<string>("SELECT LastError FROM dbo.WebhookDeliveries"));
            var seconds = await db.Scalar<int>("SELECT DATEDIFF(second,SYSUTCDATETIME(),NextAttemptAtUtc) FROM dbo.WebhookDeliveries");
            Assert.True(immediate ? seconds <= 0 : seconds > 250);
            if (attempt < 8)
            {
                if (!immediate) await db.Execute("UPDATE dbo.WebhookDeliveries SET NextAttemptAtUtc=DATEADD(second,-1,SYSUTCDATETIME())");
                delivery = Assert.Single(await db.Store.ClaimDueAsync(default));
            }
        }
        Assert.Equal(8, await db.Scalar<int>("SELECT Attempts FROM dbo.WebhookDeliveries"));
        Assert.Equal("DeadLettered", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        Assert.Equal(0, await db.Store.MakeDueAsync(1, default));
    }

    [TestCase("Pending", null, "CONNECTION_FAILED", false, 1, true)]
    [TestCase("Pending", null, "TIMEOUT:IMMEDIATE", false, 1, true)]
    [TestCase("Pending", null, "TLS_FAILED", false, 1, true)]
    [TestCase("Pending", 503, "HTTP 503", false, 1, false)]
    [TestCase("Pending", 401, "CONNECTION_FAILED", false, 1, false)]
    [TestCase("Pending", null, "INTERNAL_ERROR", false, 1, false)]
    [TestCase("Pending", null, "CONNECTION_FAILED", true, 1, false)]
    [TestCase("Delivered", null, "CONNECTION_FAILED", false, 1, false)]
    [TestCase("DeadLettered", null, "CONNECTION_FAILED", false, 1, false)]
    [TestCase("Superseded", null, "CONNECTION_FAILED", false, 1, false)]
    [TestCase("Pending", null, "CONNECTION_FAILED", false, 2, false)]
    public async Task Consumer_alive_accelerates_only_eligible_deliveries(string status, int? code, string error, bool leased, int consumer, bool due)
    {
        SqlTestGuard.Require();
        await CompletedEvent();
        await db.Execute("""
            UPDATE dbo.WebhookDeliveries SET Status=@status, LastStatusCode=@code,LastError=@error,
              LeaseToken=CASE WHEN @leased=1 THEN NEWID() END,
              LeaseExpiresAtUtc=CASE WHEN @leased=1 THEN DATEADD(minute,5,SYSUTCDATETIME()) END,
              NextAttemptAtUtc=DATEADD(hour,1,SYSUTCDATETIME())
            """, new { status, code, error, leased });
        var before = await db.Snapshot();
        Assert.Equal(due ? 1 : 0, await db.Store.MakeDueAsync((short)consumer, default));
        if (due)
        {
            Assert.Equal(1, (await db.Store.ClaimDueAsync(default)).Count);
            Assert.Equal(0, await db.Scalar<int>("SELECT Attempts FROM dbo.WebhookDeliveries"));
        }
        else Assert.Equal(before, await db.Snapshot());
    }

    [Test, Category("Sql")]
    public async Task S17_maps_both_columns_and_nullable_failed_result_and_S23_filters_callbacks()
    {
        SqlTestGuard.Require();
        var delivery = await CompletedEvent();
        Assert.Equal(new EventContext("test-correlation", "{\"value\":42}"), await db.Store.GetEventContextAsync(delivery.JobId, 1, default));
        Assert.Null(await db.Store.GetEventContextAsync(Guid.NewGuid(), 1, default));
        var job = await db.NewClaim();
        await db.Store.FailAsync(new(job.JobId, job.LeaseToken, "MODEL_NOT_FOUND", "No model.", false, Guid.NewGuid(), "{}"), default);
        Assert.Equal(new EventContext("test-correlation", null), await db.Store.GetEventContextAsync(job.JobId, 1, default));
        await db.Execute("UPDATE dbo.Consumers SET CallbackCaCertName='private-ca'");
        var consumer = Assert.Single(await db.Store.GetCallbacksAsync(default));
        Assert.Equal((short)1, consumer.ConsumerId); Assert.Equal("TEST", consumer.Code);
        Assert.Equal("https://test.example/callback", consumer.CallbackUrl); Assert.Equal("test-secret", consumer.WebhookSecretName); Assert.Equal("private-ca", consumer.CallbackCaCertName);
        await db.Execute("UPDATE dbo.Consumers SET IsActive=0"); Assert.Empty(await db.Store.GetCallbacksAsync(default));
        await db.Execute("UPDATE dbo.Consumers SET IsActive=1, CallbackUrl=NULL, WebhookSecretName=NULL, CallbackCaCertName=NULL"); Assert.Empty(await db.Store.GetCallbacksAsync(default));
    }
    [Test, Category("Sql")]
    public async Task Old_lease_records_nothing_after_a_new_claim()
    {
        SqlTestGuard.Require();
        var old = await CompletedEvent();
        await db.Execute("UPDATE dbo.WebhookDeliveries SET LeaseExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME())");
        var current = Assert.Single(await db.Store.ClaimDueAsync(default)); Assert.NotEqual(old.LeaseToken, current.LeaseToken);
        Assert.Equal(WriteOutcome.LeaseLost, await db.Store.RecordAttemptAsync(old.DeliveryId, old.LeaseToken, 200, null, default));
        Assert.Equal(0, await db.Scalar<int>("SELECT Attempts FROM dbo.WebhookDeliveries"));
    }
    [Test]
    public async Task Delivery_over_real_store_carries_result_and_marks_Delivered()
    {
        SqlTestGuard.Require();
        var delivery = await CompletedEvent(); var f = new WebhookFakes();
        await f.Processor(db.Store).ExecuteAsync(delivery);
        Assert.Equal(42, JsonNode.Parse(Assert.Single(f.Requests).Body)!["Job"]!["Result"]!["value"]!.GetValue<int>());
        Assert.Equal("test-correlation", f.Requests[0].CorrelationId);
        Assert.Equal("Delivered", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        Assert.Equal(1, await db.Scalar<int>("SELECT Attempts FROM dbo.WebhookDeliveries"));
    }
    [Test]
    public async Task Eight_failures_end_DeadLettered()
    {
        SqlTestGuard.Require();
        var delivery = await CompletedEvent(); var f = new WebhookFakes { Response = new(503) };
        for (var i = 0; i < 8; i++)
        {
            await f.Processor(db.Store).ExecuteAsync(delivery);
            if (i < 7)
            {
                await db.Execute("UPDATE dbo.WebhookDeliveries SET NextAttemptAtUtc=DATEADD(second,-1,SYSUTCDATETIME())");
                delivery = Assert.Single(await db.Store.ClaimDueAsync(default));
            }
        }
        Assert.Equal("DeadLettered", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        Assert.Equal(8, await db.Scalar<int>("SELECT Attempts FROM dbo.WebhookDeliveries"));
        Assert.Contains(f.Logs.Lines, l => l.Text.Contains("webhook.dead_lettered"));
    }
    [Test]
    public async Task Superseded_during_send_records_nothing()
    {
        SqlTestGuard.Require();
        var delivery = await CompletedEvent(); var f = new WebhookFakes();
        f.Send = async (_, _) =>
        {
            await db.Execute("UPDATE dbo.WebhookDeliveries SET Status='Superseded',LeaseToken=NULL,LeaseExpiresAtUtc=NULL");
            return new(200);
        };
        await f.Processor(db.Store).ExecuteAsync(delivery);
        Assert.Equal(0, await db.Scalar<int>("SELECT Attempts FROM dbo.WebhookDeliveries"));
        Assert.Equal("Superseded", await db.Scalar<string>("SELECT Status FROM dbo.WebhookDeliveries"));
        Assert.Contains(f.Logs.Lines, l => l.Text.Contains("webhook.lease_lost"));
    }
}
