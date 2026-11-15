using NUnit.Framework;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Data.SqlClient;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public sealed partial class PageQuotaSqlTests
{
    private async Task<string?> Cancel(IntakeRequest request, short consumer=1) =>
        (await db.Store.CancelAsync(consumer,request.ClientRequestId!,default))?.Outcome;
    private async Task AssertCancelled()
    {
        Assert.That(await db.Scalar<string>("SELECT Status+':'+ErrorCode+':'+ErrorMessage FROM dbo.Jobs"), Is.EqualTo("Failed:CANCELLED:Cancelled by the consumer."));
        Assert.That(await db.Scalar<string>("SELECT Status+':'+ErrorCode FROM dbo.JobRuns"), Is.EqualTo("Failed:CANCELLED"));
        Assert.That(await db.Scalar<int>("SELECT Attempts FROM dbo.Jobs"), Is.Zero);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.Jobs WHERE LeaseToken IS NOT NULL OR LeaseExpiresAtUtc IS NOT NULL OR CompletedAtUtc IS NULL"), Is.Zero);
        Assert.That(await db.Scalar<string>("SELECT State FROM dbo.PageReservations"), Is.EqualTo("Released"));
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"), Is.Zero);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.WebhookDeliveries"), Is.Zero);
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.UsageLedger"), Is.Zero);
    }
    [Test]
    public async Task Cancel_queued_releases_without_charge_and_same_file_requeues_new_reservation()
    {
        SqlTestGuard.Require(); await Prepaid(); await Grant(10);
        var request=SqlDatabase.Request(); await db.Store.IntakeAsync(request,default);
        Assert.That(await Cancel(request), Is.EqualTo("Cancelled"));
        var audit=await db.Scalar<DateTime>("SELECT CancelRequestedAtUtc FROM dbo.JobRuns");
        Assert.That(await Cancel(request), Is.EqualTo("Cancelled"));
        Assert.That(await db.Scalar<DateTime>("SELECT CancelRequestedAtUtc FROM dbo.JobRuns"), Is.EqualTo(audit));
        await AssertCancelled();
        var next=await db.Store.IntakeAsync(request with { ClientRequestId="new-request" },default);
        Assert.That(next.Outcome, Is.EqualTo(IntakeOutcome.Requeued)); Assert.That(next.RunNo, Is.EqualTo(2));
        Assert.That(await db.Scalar<string>("SELECT State FROM dbo.PageReservations WHERE RunNo=2"), Is.EqualTo("Reserved"));
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.PageReservations"), Is.EqualTo(2));
        Assert.That(await Cancel(request), Is.EqualTo("Cancelled"));
        Assert.That(await db.Scalar<string>("SELECT Status FROM dbo.Jobs"), Is.EqualTo("Queued"));
    }
    [Test]
    public async Task Cancel_processing_signals_50017_then_worker_finishes_without_attempt()
    {
        SqlTestGuard.Require(); var request=SqlDatabase.Request(); var job=await Claim(request);
        Assert.That(await Cancel(request), Is.EqualTo("Cancelling"));
        var error=Assert.ThrowsAsync<SqlException>(async ()=>await db.Scalar<long>(new SqlStatementCatalog()["S4a"],new { job.JobId,job.LeaseToken,kind="extract",modelId="bl-v1" }));
        Assert.That(error!.Number, Is.EqualTo(50017));
        Assert.That((await db.Store.BeginAsync(new(job.JobId,job.LeaseToken),"extract","bl-v1",default)).Outcome, Is.EqualTo(WriteOutcome.CancelRequested));
        Assert.That(await db.Store.FinishCancellationAsync(new(job.JobId,job.LeaseToken),default), Is.EqualTo(WriteOutcome.Applied));
        await AssertCancelled();
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AzureOperations"), Is.Zero);
    }
    [Test]
    public async Task Accepted_cancel_continues_completes_and_dedupes_with_one_charge()
    {
        SqlTestGuard.Require(); var request=SqlDatabase.Request(); var job=await Claim(request);
        var op=await db.Accepted(job);
        Assert.That(await Cancel(request), Is.EqualTo("Continuing"));
        Assert.That(await Cancel(request), Is.EqualTo("Continuing"));
        await db.Store.ExtractionSucceededAsync(new(job.JobId,job.LeaseToken,op,"bl-v1",1,DateTimeOffset.UtcNow,"raw"),default);
        await Complete(job);
        Assert.That(await Cancel(request), Is.EqualTo("Finished"));
        Assert.That((await db.Store.IntakeAsync(request with { ClientRequestId="later" },default)).Outcome, Is.EqualTo(IntakeOutcome.Existing));
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.CustomerPageCharges"), Is.EqualTo(1));
    }
    [Test]
    public async Task Auto_succeeded_classify_continues_and_extract_can_be_inserted()
    {
        SqlTestGuard.Require();
        await db.Execute("INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status) VALUES('auto-cancel','AUTO','classify',1,'Active'); INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode) VALUES('auto-cancel','bill','BL');");
        var request=SqlDatabase.Request("AUTO"); var job=await Claim(request);
        var op=await db.Accepted(job,"classify","auto-cancel");
        await db.Store.ClassifierSucceededAsync(new(job.JobId,job.LeaseToken,op,"auto-cancel",1,DateTimeOffset.UtcNow,"bill",.99m,null,null,Guid.NewGuid(),"{}","BL"),default);
        Assert.That(await Cancel(request), Is.EqualTo("Continuing"));
        Assert.That((await db.Store.BeginAsync(new(job.JobId,job.LeaseToken),"extract","bl-v1",default)).Outcome, Is.EqualTo(WriteOutcome.Applied));
    }
    [TestCase("Unknown")]
    [TestCase("Sending")]
    [TestCase("Failed")]
    public async Task Operation_state_controls_cancellation(string state)
    {
        SqlTestGuard.Require(); var request=SqlDatabase.Request(); var job=await Claim(request);
        var op=await db.Store.BeginAsync(new(job.JobId,job.LeaseToken),"extract","bl-v1",default);
        if(state!="Sending") await db.Store.CloseSendingAsync(op.OperationId!.Value,job.LeaseToken,state,null,default);
        Assert.That(await Cancel(request), Is.EqualTo(state=="Failed" ? "Cancelling" : "Continuing"));
    }
    [Test]
    public async Task Foreign_unknown_and_finished_bindings_are_safe()
    {
        SqlTestGuard.Require(); var request=SqlDatabase.Request(); var job=await Claim(request);
        Assert.That(await Cancel(request,2), Is.Null);
        Assert.That(await Cancel(request with { ClientRequestId="unknown" }), Is.Null);
        await Fail(job);
        Assert.That(await Cancel(request), Is.EqualTo("Finished"));
        Assert.That(await db.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE CancelRequestedAtUtc IS NOT NULL"), Is.Zero);
        await db.Store.IntakeAsync(request with { ClientRequestId="retry" },default);
        Assert.That(await Cancel(request), Is.EqualTo("Finished"));
        Assert.That(await db.Scalar<string>("SELECT Status FROM dbo.Jobs"), Is.EqualTo("Queued"));
    }
}
