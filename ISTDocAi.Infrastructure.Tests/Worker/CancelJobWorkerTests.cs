using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Worker;
public sealed class CancelJobWorkerTests
{
    [Test]
    public async Task Auto_succeeded_classify_ignores_later_cancel_and_posts_extract()
    {
        var f=AutoManifestWorkerTests.Auto("BL");
        f.AfterClassification=()=>f.CancelRequested=true;
        await f.Processor().ExecuteAsync(f.Job,default);
        Assert.That(f.CancelRequested, Is.True);
        Assert.That(f.Calls, Does.Contain("post:bl-v1"));
        Assert.That(f.Calls, Does.Not.Contain("cancel"));
        Assert.That(f.Completion, Is.Not.Null);
    }
    [Test]
    public async Task Cancellation_signal_stops_before_post_without_attempt_or_webhook()
    {
        var f = new WorkerFakes(); f.CancelRequested=true;
        await f.Processor().ExecuteAsync(f.Job,default);
        Assert.That(f.Calls, Does.Contain("cancel"));
        Assert.That(f.Calls.Any(c=>c.StartsWith("post:")), Is.False);
        Assert.That(f.Calls, Does.Not.Contain("fail"));
        Assert.That(f.Completion, Is.Null);
        Assert.That(f.Failure!.ErrorCode, Is.EqualTo("CANCELLED"));
        Assert.That(f.Failure.CountsAsAttempt, Is.False);
        Assert.That(f.Failure.PayloadJson, Is.Empty);
    }
}
