using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class WebhookTests
{
    [TestCase("CONNECTION_FAILED")]
    [TestCase("TIMEOUT")]
    [TestCase("TLS_FAILED")]
    public void Connection_failures_alternate_immediate_and_existing_backoff_and_stop_at_eight(string error)
    {
        var d = DocAiServices.WebhookDeliveryService.Create(1, Fixture.Now).Value!;
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var now = d.NextAttemptAtUtc;
            var claimed = DocAiServices.WebhookDeliveryService.Claim(d, Guid.NewGuid(), now).Value!;
            d = DocAiServices.WebhookDeliveryService.RecordAttempt(claimed, claimed.LeaseToken!.Value, null, error, now).Value!;
            var immediate = attempt < 8 && attempt % 2 == 1;
            Assert.That(d.NextAttemptAtUtc, Is.EqualTo(immediate ? now : now + DocAiServices.WebhookPolicyService.DelayAfterAttempt(attempt)));
            Assert.That(d.LastError, Is.EqualTo(error + (immediate ? ":IMMEDIATE" : "")));
            Assert.That(d.Attempts, Is.EqualTo(attempt));
            Assert.That(d.Status, Is.EqualTo(attempt == 8 ? WebhookDeliveryStatus.DeadLettered : WebhookDeliveryStatus.Pending));
        }
    }

    [TestCase(400)] [TestCase(401)] [TestCase(503)]
    public void Http_response_never_schedules_an_immediate_retry(int status)
    {
        var d = DocAiServices.WebhookDeliveryService.Create(1, Fixture.Now).Value!;
        d = DocAiServices.WebhookDeliveryService.Claim(d, Fixture.Token, Fixture.Now).Value!;
        d = DocAiServices.WebhookDeliveryService.RecordAttempt(d, Fixture.Token, status, "CONNECTION_FAILED", Fixture.Now).Value!;
        Assert.That(d.NextAttemptAtUtc, Is.EqualTo(Fixture.Now.AddMinutes(1)));
        Assert.That(d.LastError, Is.EqualTo("CONNECTION_FAILED"));
    }

    [TestCase(1, 1)]
    [TestCase(2, 5)]
    [TestCase(3, 15)]
    [TestCase(4, 60)]
    [TestCase(5, 360)]
    [TestCase(6, 360)]
    [TestCase(7, 360)]
    [TestCase(8, 360)]
    public void Schedule_is_relative_to_each_attempt(int attempt, int minutes) => Assert.That(DocAiServices.WebhookPolicyService.DelayAfterAttempt(attempt), Is.EqualTo(TimeSpan.FromMinutes(minutes)));
    [Test]
    public void Eighth_failure_deadletters_and_current_run_replay_resets_schedule()
    {
        var d = DocAiServices.WebhookDeliveryService.Create(2, Fixture.Now).Value!;
        int[] minutes = [1, 5, 15, 60, 360, 360, 360, 360];
        for (var i = 0; i < 8; i++)
        {
            var now = d.NextAttemptAtUtc;
            d = DocAiServices.WebhookDeliveryService.RecordAttempt(DocAiServices.WebhookDeliveryService.Claim(d, Fixture.Token, now).Value!, Fixture.Token, i % 2 == 0 ? 302 : null, "unavailable", now).Value!;
            Assert.That(d.Attempts, Is.EqualTo(i + 1));
            Assert.That(d.NextAttemptAtUtc, Is.EqualTo(now.AddMinutes(minutes[i])));
            Assert.That(d.Status, Is.EqualTo(i == 7 ? WebhookDeliveryStatus.DeadLettered : WebhookDeliveryStatus.Pending));
            Assert.That(d.LeaseToken, Is.Null);
            Assert.That(d.DeliveredAtUtc, Is.Null);
            if (i < 7)
                Assert.That(DocAiServices.WebhookDeliveryService.Claim(d, Guid.NewGuid(), d.NextAttemptAtUtc.AddTicks(-1)).IsSuccess, Is.False);
        }

        Assert.That(DocAiServices.WebhookDeliveryService.Replay(d, 1, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.WebhookDeliveryService.Replay(d, 3, Fixture.Now).IsSuccess, Is.False);
        var replay = DocAiServices.WebhookDeliveryService.Replay(d, 2, Fixture.Now).Value!;
        Assert.That(replay.Status, Is.EqualTo(WebhookDeliveryStatus.Pending));
        Assert.That(replay.Attempts, Is.EqualTo(0));
        Assert.That(replay.LastError, Is.Null);
        Assert.That(replay.NextAttemptAtUtc, Is.EqualTo(Fixture.Now));
        Assert.That(replay.RunNo, Is.EqualTo(2));
        Assert.That(d.Attempts, Is.EqualTo(8));
    }

    [TestCase(199, false)]
    [TestCase(200, true)]
    [TestCase(204, true)]
    [TestCase(299, true)]
    [TestCase(300, false)]
    [TestCase(301, false)]
    [TestCase(302, false)]
    [TestCase(307, false)]
    [TestCase(308, false)]
    [TestCase(400, false)]
    [TestCase(500, false)]
    [TestCase(null, false)]
    public void Only_2xx_delivers(int? http, bool delivered)
    {
        var d = DocAiServices.WebhookDeliveryService.RecordAttempt(DocAiServices.WebhookDeliveryService.Claim(DocAiServices.WebhookDeliveryService.Create(1, Fixture.Now).Value!, Fixture.Token, Fixture.Now).Value!, Fixture.Token, http, null, Fixture.Now).Value!;
        Assert.That(DocAiServices.WebhookPolicyService.IsDelivered(http), Is.EqualTo(delivered));
        Assert.That(d.Status, Is.EqualTo(delivered ? WebhookDeliveryStatus.Delivered : WebhookDeliveryStatus.Pending));
        Assert.That(d.DeliveredAtUtc, Is.EqualTo(delivered ? Fixture.Now : (DateTimeOffset? )null));
        Assert.That(d.Attempts, Is.EqualTo(1));
        Assert.That(d.LastStatusCode, Is.EqualTo(http));
    }

    [Test]
    public void Success_on_eighth_attempt_delivers()
    {
        var d = DocAiServices.WebhookDeliveryService.Create(1, Fixture.Now).Value!;
        for (var i = 1; i <= 8; i++)
            d = DocAiServices.WebhookDeliveryService.RecordAttempt(DocAiServices.WebhookDeliveryService.Claim(d, Fixture.Token, d.NextAttemptAtUtc).Value!, Fixture.Token, i == 8 ? 200 : 500, null, d.NextAttemptAtUtc).Value!;
        Assert.That(d.Status, Is.EqualTo(WebhookDeliveryStatus.Delivered));
        Assert.That(d.Attempts, Is.EqualTo(8));
    }

    [TestCase(WebhookDeliveryStatus.Pending)]
    [TestCase(WebhookDeliveryStatus.Delivered)]
    [TestCase(WebhookDeliveryStatus.DeadLettered)]
    [TestCase(WebhookDeliveryStatus.Superseded)]
    public void Terminal_deliveries_cannot_be_claimed_or_sent_or_superseded(WebhookDeliveryStatus status)
    {
        var d = Fixture.Delivery(status);
        Assert.That(DocAiServices.WebhookDeliveryService.Claim(d, Fixture.Token, d.NextAttemptAtUtc).IsSuccess, Is.EqualTo(status == WebhookDeliveryStatus.Pending));
        Assert.That(DocAiServices.WebhookDeliveryService.Supersede(d, 2).IsSuccess, Is.EqualTo(status == WebhookDeliveryStatus.Pending));
        Assert.That(DocAiServices.WebhookDeliveryService.Replay(d, 1, Fixture.Now).IsSuccess, Is.EqualTo(status == WebhookDeliveryStatus.DeadLettered));
        Assert.That(DocAiServices.WebhookDeliveryService.RecordAttempt(d, Fixture.Token, 200, null, Fixture.Now).Rejection, Is.EqualTo(Rejection.LeaseLost));
        Assert.That(DocAiServices.WebhookDeliveryService.Supersede(d, 1).IsSuccess, Is.False);
    }

    [Test]
    public void Superseding_an_inflight_event_clears_lease_and_rejects_old_sender()
    {
        var d = DocAiServices.WebhookDeliveryService.Claim(DocAiServices.WebhookDeliveryService.Create(1, Fixture.Now).Value!, Fixture.Token, Fixture.Now).Value!;
        d = DocAiServices.WebhookDeliveryService.Supersede(d, 2).Value!;
        Assert.That(d.LeaseToken, Is.Null);
        Assert.That(d.LeaseExpiresAtUtc, Is.Null);
        Assert.That(DocAiServices.WebhookDeliveryService.RecordAttempt(d, Fixture.Token, 200, null, Fixture.Now).Rejection, Is.EqualTo(Rejection.LeaseLost));
    }

    [Test]
    public void Webhook_token_fences_but_does_not_require_unexpired_lease()
    {
        var d = DocAiServices.WebhookDeliveryService.Claim(DocAiServices.WebhookDeliveryService.Create(1, Fixture.Now).Value!, Fixture.Token, Fixture.Now).Value!;
        Assert.That(d.LeaseExpiresAtUtc, Is.EqualTo(Fixture.Now.AddMinutes(5)));
        Assert.That(DocAiServices.WebhookDeliveryService.Claim(d, Guid.NewGuid(), Fixture.Now.AddMinutes(5)).IsSuccess, Is.False);
        Assert.That(DocAiServices.WebhookDeliveryService.RecordAttempt(d, Fixture.Token, 200, null, Fixture.Now.AddMinutes(6)).IsSuccess, Is.True);
        var replacement = Guid.NewGuid();
        d = DocAiServices.WebhookDeliveryService.Claim(d, replacement, Fixture.Now.AddMinutes(6)).Value!;
        Assert.That(DocAiServices.WebhookDeliveryService.RecordAttempt(d, Fixture.Token, 200, null, Fixture.Now).Rejection, Is.EqualTo(Rejection.LeaseLost));
        Assert.That(DocAiServices.WebhookDeliveryService.RecordAttempt(d, replacement, 200, null, Fixture.Now).IsSuccess, Is.True);
    }

    [Test]
    public void Invalid_ids_and_tokens_rejected()
    {
        Assert.That(DocAiServices.WebhookDeliveryService.Create(0, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.WebhookDeliveryService.Create(32768, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.WebhookDeliveryService.Claim(DocAiServices.WebhookDeliveryService.Create(1, Fixture.Now).Value!, Guid.Empty, Fixture.Now).IsSuccess, Is.False);
    }
}
