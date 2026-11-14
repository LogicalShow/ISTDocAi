using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class ReviewRegressionTests
{
    [TestCase(false, false, true, RunReason.Requeue)]
    [TestCase(false, true, true, RunReason.Requeue)]
    [TestCase(true, false, false, RunReason.Reprocess)]
    [TestCase(true, true, true, RunReason.Reprocess)]
    public void New_run_reason_follows_S2(bool completed, bool reprocessRequested, bool allowed, RunReason expectedReason)
    {
        var job = completed ? DocAiServices.JobLifecycleService.Complete(Fixture.Mapping(), Fixture.Token, Fixture.Now).Value! : DocAiServices.JobLifecycleService.Fail(Fixture.Processing(), Fixture.Token, new(ErrorCodes.InvalidDocument, "bad"), Fixture.Now).Value!;
        var decision = DocAiServices.JobLifecycleService.StartNewRun(job, reprocessRequested, true, Fixture.Now);
        Assert.That(decision.IsSuccess, Is.EqualTo(allowed));
        if (allowed)
        {
            Assert.That(decision.Value!.CurrentRun.Reason, Is.EqualTo(expectedReason));
            Assert.That(decision.Value.CurrentRun.RunNo, Is.EqualTo(2));
        }
        else
        {
            Assert.That(decision.Rejection, Is.EqualTo(Rejection.InvalidTransition));
            Assert.That(decision.Value, Is.Null);
        }
    }

    [TestCase(1L)]
    [TestCase(2147483648L)]
    [TestCase(long.MaxValue)]
    public void Caller_supplied_database_identity_is_preserved(long operationId)
    {
        var decision = DocAiServices.JobLifecycleService.BeginOperation(Fixture.Processing(), Fixture.Token, operationId, OperationKind.Extract, "model", Fixture.Now);
        Assert.That(decision.IsSuccess, Is.True);
        Assert.That(PortAssertions.Single(decision.Value!.Operations).OperationId, Is.EqualTo(operationId));
    }

    [TestCase(0L)]
    [TestCase(-1L)]
    [TestCase(long.MinValue)]
    public void Nonpositive_operation_identity_is_rejected(long operationId)
    {
        Assert.That(DocAiServices.AzureOperationLifecycleService.Start(operationId, 1, OperationKind.Extract, "model", [], Fixture.Now).Rejection, Is.EqualTo(Rejection.InvalidInput));
        Assert.That(DocAiServices.JobLifecycleService.BeginOperation(Fixture.Processing(), Fixture.Token, operationId, OperationKind.Extract, "model", Fixture.Now).Rejection, Is.EqualTo(Rejection.InvalidInput));
    }
}
