using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class StateMachineTests
{
    public static IEnumerable<object[]> JobTransitions() => Pairs<JobStatus>().Select(p => new object[] { p.Item1, p.Item2, p is (JobStatus.Queued, JobStatus.Processing) or (JobStatus.Processing, JobStatus.Queued or JobStatus.Completed or JobStatus.Failed) });
    public static IEnumerable<object[]> RunTransitions() => Pairs<RunStatus>().Select(p => new object[] { p.Item1, p.Item2, p.Item1 == RunStatus.Active && p.Item2 is RunStatus.Completed or RunStatus.Failed });
    public static IEnumerable<object[]> StageTransitions() => Pairs<JobStage>().Select(p => new object[] { p.Item1, p.Item2, p is (JobStage.Classify, JobStage.Extract) or (JobStage.Extract, JobStage.Map) });
    public static IEnumerable<object[]> OperationTransitions() => Pairs<AzureOperationState>().Select(p => new object[] { p.Item1, p.Item2, p is (AzureOperationState.Sending, AzureOperationState.Accepted or AzureOperationState.Failed or AzureOperationState.Unknown) or (AzureOperationState.Accepted, AzureOperationState.Succeeded or AzureOperationState.Failed) });
    public static IEnumerable<object[]> WebhookTransitions() =>
        from p in Pairs<WebhookDeliveryStatus>()from current in new[]
        {
            false,
            true
        }

        select new object[]
        {
            p.Item1,
            p.Item2,
            current,
            p.Item1 == WebhookDeliveryStatus.Pending && p.Item2 is WebhookDeliveryStatus.Delivered or WebhookDeliveryStatus.DeadLettered or WebhookDeliveryStatus.Superseded || p == (WebhookDeliveryStatus.DeadLettered, WebhookDeliveryStatus.Pending) && current
        };
    private static IEnumerable<(T, T)> Pairs<T>()
        where T : struct, Enum =>
        from a in Enum.GetValues<T>()from b in Enum.GetValues<T>()select (a, b);
    [TestCaseSource(nameof(JobTransitions))]
    public void Job_matrix(JobStatus from, JobStatus to, bool expected) => Assert.That(DocAiServices.StateMachinesService.CanTransition(from, to), Is.EqualTo(expected));
    [TestCaseSource(nameof(RunTransitions))]
    public void Run_matrix(RunStatus from, RunStatus to, bool expected)
    {
        Assert.That(DocAiServices.StateMachinesService.CanTransition(from, to), Is.EqualTo(expected));
        var run = new JobRunSnapshot(1, RunReason.New, from, Fixture.Now);
        var result = DocAiServices.JobRunLifecycleService.Finish(run, to, Fixture.Now.AddMinutes(1), new(ErrorCodes.InvalidDocument, "error"));
        Assert.That(result.IsSuccess, Is.EqualTo(expected));
        if (expected)
            Assert.That(result.Value!.CompletedAtUtc, Is.EqualTo(Fixture.Now.AddMinutes(1)));
        else
            Assert.That(result.Rejection, Is.EqualTo(Rejection.InvalidTransition));
    }

    [TestCaseSource(nameof(StageTransitions))]
    public void Stage_matrix(JobStage from, JobStage to, bool expected) => Assert.That(DocAiServices.StateMachinesService.CanTransition(from, to), Is.EqualTo(expected));
    [TestCaseSource(nameof(OperationTransitions))]
    public void Operation_matrix(AzureOperationState from, AzureOperationState to, bool expected)
    {
        Assert.That(DocAiServices.StateMachinesService.CanTransition(from, to), Is.EqualTo(expected));
        var op = Fixture.Operation(from);
        var decision = to == AzureOperationState.Accepted ? DocAiServices.AzureOperationLifecycleService.Accept(op, "https://azure/result", Fixture.Now) : DocAiServices.AzureOperationLifecycleService.Transition(op, to, Fixture.Now);
        Assert.That(decision.IsSuccess, Is.EqualTo(expected));
        if (expected)
            Assert.That(decision.Value!.State, Is.EqualTo(to));
        else
            Assert.That(decision.Rejection, Is.EqualTo(Rejection.InvalidTransition));
    }

    [TestCaseSource(nameof(WebhookTransitions))]
    public void Webhook_matrix(WebhookDeliveryStatus from, WebhookDeliveryStatus to, bool current, bool expected) => Assert.That(DocAiServices.StateMachinesService.CanTransition(from, to, current), Is.EqualTo(expected));
    [TestCase(null, false, JobStage.Classify)]
    [TestCase(null, true, JobStage.Extract)]
    [TestCase("MAERSK", false, JobStage.Extract)]
    [TestCase("MAERSK", true, JobStage.Extract)]
    public void Starting_stage(string? line, bool generic, JobStage expected) => Assert.That(DocAiServices.JobLifecycleService.Create(line, generic, Fixture.Now).Stage, Is.EqualTo(expected));
    [Test]
    public void Failure_requires_error() => Assert.That(DocAiServices.JobRunLifecycleService.Finish(new JobRunSnapshot(1, RunReason.New, RunStatus.Active, Fixture.Now), RunStatus.Failed, Fixture.Now).IsSuccess, Is.False);
}

internal static class Fixture
{
    // Simulate database-assigned bigint identities, including values beyond Int32.
    private static long operationId = int.MaxValue;
    internal static long NextOperationId() => Interlocked.Increment(ref operationId);
    internal static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    internal static readonly Guid Token = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static JobSnapshot Processing(bool classify = false) => DocAiServices.JobLifecycleService.Claim(DocAiServices.JobLifecycleService.Create(null, !classify, Now), Token, Now).Value!;
    internal static AzureOperationSnapshot Operation(AzureOperationState state, int run = 1, OperationKind kind = OperationKind.Extract)
    {
        var op = DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), run, kind, "model", [], Now).Value!;
        if (state == AzureOperationState.Sending)
            return op;
        if (state is AzureOperationState.Unknown or AzureOperationState.Failed)
            return DocAiServices.AzureOperationLifecycleService.Transition(op, state, Now).Value!;
        op = DocAiServices.AzureOperationLifecycleService.Accept(op, "https://azure/result", Now).Value!;
        return state == AzureOperationState.Accepted ? op : DocAiServices.AzureOperationLifecycleService.Transition(op, state, Now).Value!;
    }

    internal static JobSnapshot Accepted(JobSnapshot job, out long id)
    {
        id = Fixture.NextOperationId();
        return DocAiServices.JobLifecycleService.RecordAccepted(DocAiServices.JobLifecycleService.BeginOperation(job, Token, id, job.Stage == JobStage.Classify ? OperationKind.Classify : OperationKind.Extract, "model", Now).Value!, id, "https://azure/result", Now).Value!;
    }

    internal static JobSnapshot Mapping()
    {
        var j = Accepted(Processing(), out var id);
        return DocAiServices.JobLifecycleService.ExtractionSucceeded(j, Token, id, "raw/path", Now).Value!;
    }

    internal static WebhookDeliverySnapshot Delivery(WebhookDeliveryStatus status)
    {
        var d = DocAiServices.WebhookDeliveryService.Create(1, Now).Value!;
        if (status == WebhookDeliveryStatus.Superseded)
            return DocAiServices.WebhookDeliveryService.Supersede(d, 2).Value!;
        if (status == WebhookDeliveryStatus.Delivered)
            return DocAiServices.WebhookDeliveryService.RecordAttempt(DocAiServices.WebhookDeliveryService.Claim(d, Token, Now).Value!, Token, 200, null, Now).Value!;
        if (status == WebhookDeliveryStatus.DeadLettered)
            for (var i = 0; i < 8; i++)
                d = DocAiServices.WebhookDeliveryService.RecordAttempt(DocAiServices.WebhookDeliveryService.Claim(d, Token, d.NextAttemptAtUtc).Value!, Token, 500, "failure", d.NextAttemptAtUtc).Value!;
        return d;
    }
}
