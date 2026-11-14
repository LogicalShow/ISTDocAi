using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class JobTests
{
    [Test]
    public void New_job_has_run_one_and_no_lease()
    {
        var j = DocAiServices.JobLifecycleService.Create("MAERSK", false, Fixture.Now);
        Assert.That(j.Status, Is.EqualTo(JobStatus.Queued));
        Assert.That(j.CurrentRun.RunNo, Is.EqualTo(1));
        Assert.That(j.CurrentRun.Reason, Is.EqualTo(RunReason.New));
        Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Active));
        Assert.That(j.CurrentRun.CreatedAtUtc, Is.EqualTo(Fixture.Now));
        Assert.That(j.LeaseToken, Is.Null);
        Assert.That(j.Operations, Is.Empty);
        Assert.That(j.Attempts, Is.EqualTo(0));
        Assert.That(j.TakeoverCount, Is.EqualTo(0));
    }

    [Test]
    public void Claim_and_renew_use_sixty_seconds_and_token_fences_without_expiry_check()
    {
        var j = Fixture.Processing();
        Assert.That(j.LeaseExpiresAtUtc, Is.EqualTo(Fixture.Now.AddSeconds(60)));
        Assert.That(DocAiServices.JobLifecycleService.Claim(j, Guid.NewGuid(), Fixture.Now.AddSeconds(60)).IsSuccess, Is.False); // SQL uses strictly less
        Assert.That(DocAiServices.JobLifecycleService.Renew(j, Fixture.Token, Fixture.Now.AddMinutes(20)).Value!.LeaseExpiresAtUtc, Is.EqualTo(Fixture.Now.AddMinutes(21)));
        Assert.That(DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, Fixture.Now.AddMinutes(11)).IsSuccess, Is.False);
        var t = Guid.NewGuid();
        var takeover = DocAiServices.JobLifecycleService.Claim(j, t, Fixture.Now.AddMinutes(11)).Value!;
        Assert.That(takeover.TakeoverCount, Is.EqualTo(1));
        Assert.That(takeover.CurrentRun.RunNo, Is.EqualTo(1));
        Assert.That(DocAiServices.JobLifecycleService.Renew(takeover, Fixture.Token, Fixture.Now).Rejection, Is.EqualTo(Rejection.LeaseLost));
        Assert.That(DocAiServices.JobLifecycleService.Complete(takeover, Fixture.Token, Fixture.Now).Rejection, Is.EqualTo(Rejection.LeaseLost));
    }

    [Test]
    public void Third_takeover_fails_without_attempt_or_new_run()
    {
        var j = Fixture.Processing();
        for (var i = 1; i <= 3; i++)
        {
            var token = Guid.NewGuid();
            var now = Fixture.Now.AddMinutes(11 * i);
            j = DocAiServices.JobLifecycleService.Claim(j, token, now).Value!;
            Assert.That(j.TakeoverCount, Is.EqualTo(i));
            Assert.That(DocAiServices.JobLifecycleService.Resume(j), Is.EqualTo(i >= 3 ? ResumeAction.FailCrashLoop : ResumeAction.Submit));
            if (i == 3)
                Assert.That(DocAiServices.JobLifecycleService.BeginOperation(j, token, Fixture.NextOperationId(), OperationKind.Extract, "model", now).IsSuccess, Is.False);
            j = DocAiServices.JobLifecycleService.EnforceTakeoverBudget(j, token, now).Value!;
        }

        Assert.That(j.Status, Is.EqualTo(JobStatus.Failed));
        Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Failed));
        Assert.That(j.Error!.Code, Is.EqualTo(ErrorCodes.WorkerCrashLoop));
        Assert.That(j.Attempts, Is.EqualTo(0));
        Assert.That(j.CurrentRun.RunNo, Is.EqualTo(1));
    }

    [Test]
    public void Retry_preserves_accepted_operation_and_stage_and_fifth_failure_finishes_run()
    {
        var j = Fixture.Accepted(Fixture.Processing(), out var id);
        for (var failure = 1; failure <= 5; failure++)
        {
            j = DocAiServices.JobLifecycleService.Retry(j, Fixture.Token, Fixture.Now).Value!;
            Assert.That(j.Attempts, Is.EqualTo(failure));
            Assert.That(j.CurrentRun.RunNo, Is.EqualTo(1));
            Assert.That(j.Operations.Single(o => o.OperationId == id).State, Is.EqualTo(AzureOperationState.Accepted));
            if (failure < 5)
            {
                Assert.That(j.Status, Is.EqualTo(JobStatus.Queued));
                Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Active));
                Assert.That(DocAiServices.JobLifecycleService.Claim(j, Guid.NewGuid(), j.NextAttemptAtUtc.AddTicks(-1)).IsSuccess, Is.False);
                j = DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, j.NextAttemptAtUtc).Value!;
                Assert.That(DocAiServices.JobLifecycleService.Resume(j), Is.EqualTo(ResumeAction.Poll));
                Assert.That(j.TakeoverCount, Is.EqualTo(0));
            }
        }

        Assert.That(j.Status, Is.EqualTo(JobStatus.Failed));
        Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Failed));
        Assert.That(j.Error!.Code, Is.EqualTo(ErrorCodes.RetriesExhausted));
        Assert.That(j.LeaseToken, Is.Null);
    }

    [Test]
    public void Release_uses_no_attempt_and_does_not_count_as_takeover()
    {
        var j = DocAiServices.JobLifecycleService.Release(Fixture.Processing(), Fixture.Token, Fixture.Now, TimeSpan.FromSeconds(301)).Value!;
        Assert.That(j.NextAttemptAtUtc, Is.EqualTo(Fixture.Now.AddSeconds(301)));
        Assert.That(j.Error, Is.Null);
        Assert.That(j.LeaseToken, Is.Null);
        j = DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, j.NextAttemptAtUtc).Value!;
        Assert.That(j.TakeoverCount, Is.EqualTo(0));
        Assert.That(j.Attempts, Is.EqualTo(0));
        Assert.That(j.CurrentRun.RunNo, Is.EqualTo(1));
        var config = DocAiServices.JobLifecycleService.Release(j, Fixture.Token, Fixture.Now, TimeSpan.FromMinutes(5), ErrorCodes.AzureAuth).Value!;
        Assert.That(config.Error!.Code, Is.EqualTo(ErrorCodes.AzureAuth));
        Assert.That(config.Attempts, Is.EqualTo(0));
        Assert.That(DocAiServices.JobLifecycleService.Release(j, Fixture.Token, Fixture.Now, TimeSpan.FromSeconds(-1)).IsSuccess, Is.False);
    }

    [Test]
    public void Wrong_token_rejects_all_guarded_job_writes()
    {
        var j = Fixture.Accepted(Fixture.Processing(), out var id);
        var stale = Guid.NewGuid();
        Decision<JobSnapshot>[] outcomes = [DocAiServices.JobLifecycleService.Renew(j, stale, Fixture.Now), DocAiServices.JobLifecycleService.Release(j, stale, Fixture.Now, TimeSpan.Zero), DocAiServices.JobLifecycleService.Retry(j, stale, Fixture.Now), DocAiServices.JobLifecycleService.Fail(j, stale, new(ErrorCodes.InternalError, "x"), Fixture.Now), DocAiServices.JobLifecycleService.EnforceTakeoverBudget(j, stale, Fixture.Now), DocAiServices.JobLifecycleService.BeginOperation(j, stale, Fixture.NextOperationId(), OperationKind.Extract, "m", Fixture.Now), DocAiServices.JobLifecycleService.CloseSending(j, stale, id, AzureOperationState.Unknown, Fixture.Now), DocAiServices.JobLifecycleService.ClassifierSucceeded(j, stale, id, "line", 1, .8, Fixture.Now), DocAiServices.JobLifecycleService.ExtractionSucceeded(j, stale, id, "raw", Fixture.Now), DocAiServices.JobLifecycleService.Complete(j, stale, Fixture.Now)];
        PortAssertions.All(outcomes, d =>
        {
            Assert.That(d.Rejection, Is.EqualTo(Rejection.LeaseLost));
            Assert.That(d.Value, Is.Null);
        });
        Assert.That(j.Status, Is.EqualTo(JobStatus.Processing));
        Assert.That(j.Operations[0].State, Is.EqualTo(AzureOperationState.Accepted));
    }

    [Test]
    public void Accepted_202_is_recorded_after_release_without_token_and_cannot_become_unknown()
    {
        var id = Fixture.NextOperationId();
        var j = DocAiServices.JobLifecycleService.BeginOperation(Fixture.Processing(), Fixture.Token, id, OperationKind.Extract, "m", Fixture.Now).Value!;
        Assert.That(DocAiServices.JobLifecycleService.Resume(j), Is.EqualTo(ResumeAction.CloseUnknown));
        j = DocAiServices.JobLifecycleService.RecordAccepted(DocAiServices.JobLifecycleService.Release(j, Fixture.Token, Fixture.Now, TimeSpan.Zero).Value!, id, "https://azure/result", Fixture.Now).Value!;
        j = DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, Fixture.Now).Value!;
        Assert.That(DocAiServices.JobLifecycleService.Resume(j), Is.EqualTo(ResumeAction.Poll));
        Assert.That(DocAiServices.JobLifecycleService.CloseSending(j, Fixture.Token, id, AzureOperationState.Unknown, Fixture.Now).Rejection, Is.EqualTo(Rejection.OperationNotOpen));
        Assert.That(DocAiServices.JobLifecycleService.RecordAccepted(j, id, "https://other", Fixture.Now).IsSuccess, Is.False);
    }

    [Test]
    public void Two_unknown_calls_refuse_third_post_across_kinds()
    {
        var j = Fixture.Processing();
        for (var i = 0; i < 2; i++)
        {
            var id = Fixture.NextOperationId();
            j = DocAiServices.JobLifecycleService.BeginOperation(j, Fixture.Token, id, OperationKind.Extract, "m", Fixture.Now).Value!;
            j = DocAiServices.JobLifecycleService.CloseSending(j, Fixture.Token, id, AzureOperationState.Unknown, Fixture.Now).Value!;
        }

        Assert.That(DocAiServices.JobLifecycleService.Resume(j), Is.EqualTo(ResumeAction.FailRetriesExhausted));
        var rejected = DocAiServices.JobLifecycleService.BeginOperation(j, Fixture.Token, Fixture.NextOperationId(), OperationKind.Extract, "m", Fixture.Now);
        Assert.That(rejected.Rejection, Is.EqualTo(Rejection.RetriesExhausted));
        Assert.That(rejected.Detail, Is.EqualTo(ErrorCodes.RetriesExhausted));
        j = DocAiServices.JobLifecycleService.Fail(j, Fixture.Token, new(ErrorCodes.RetriesExhausted, "Two Unknown calls."), Fixture.Now).Value!;
        Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Failed));
        var unknown = new[]
        {
            Fixture.Operation(AzureOperationState.Unknown, kind: OperationKind.Classify),
            Fixture.Operation(AzureOperationState.Unknown)
        };
        Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 1, OperationKind.Extract, "m", unknown, Fixture.Now).Rejection, Is.EqualTo(Rejection.RetriesExhausted));
        Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 2, OperationKind.Extract, "m", unknown, Fixture.Now).IsSuccess, Is.True);
    }

    [Test]
    public void Open_operation_uniqueness_scoped_to_run_and_kind()
    {
        foreach (var state in new[]
        {
            AzureOperationState.Sending,
            AzureOperationState.Accepted
        }

        )
        {
            var existing = new[]
            {
                Fixture.Operation(state)
            };
            Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 1, OperationKind.Extract, "m", existing, Fixture.Now).Rejection, Is.EqualTo(Rejection.OpenOperationExists));
            Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 2, OperationKind.Extract, "m", existing, Fixture.Now).IsSuccess, Is.True);
            Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 1, OperationKind.Classify, "m", existing, Fixture.Now).IsSuccess, Is.True);
        }

        foreach (var state in new[]
        {
            AzureOperationState.Succeeded,
            AzureOperationState.Failed,
            AzureOperationState.Unknown
        }

        )
            Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 1, OperationKind.Extract, "m", [Fixture.Operation(state)], Fixture.Now).IsSuccess, Is.True);
    }

    [TestCase("MAERSK", .8, JobStatus.Processing)]
    [TestCase("MAERSK", .79, JobStatus.Failed)]
    [TestCase(null, 1, JobStatus.Failed)]
    public void Classifier_success_keeps_billed_operation_even_if_unclassified(string? line, double confidence, JobStatus expected)
    {
        var j = DocAiServices.JobLifecycleService.ClassifierSucceeded(Fixture.Accepted(Fixture.Processing(true), out var id), Fixture.Token, id, line, confidence, .8, Fixture.Now).Value!;
        Assert.That(j.Status, Is.EqualTo(expected));
        Assert.That(j.Operations[0].State, Is.EqualTo(AzureOperationState.Succeeded));
        if (expected == JobStatus.Failed)
        {
            Assert.That(j.Error!.Code, Is.EqualTo(ErrorCodes.Unclassified));
            Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Failed));
        }
        else
        {
            Assert.That(j.Stage, Is.EqualTo(JobStage.Extract));
            Assert.That(j.ShippingLineCode, Is.EqualTo(line));
            Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Active));
        }
    }

    [Test]
    public void Extract_map_complete_and_mapping_retry_never_reposts()
    {
        var j = Fixture.Processing();
        Assert.That(DocAiServices.JobLifecycleService.Complete(j, Fixture.Token, Fixture.Now).IsSuccess, Is.False);
        j = Fixture.Accepted(j, out var id);
        Assert.That(DocAiServices.JobLifecycleService.ClassifierSucceeded(j, Fixture.Token, id, "line", 1, .8, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.ExtractionSucceeded(j, Fixture.Token, id, "", Fixture.Now).IsSuccess, Is.False);
        j = DocAiServices.JobLifecycleService.ExtractionSucceeded(j, Fixture.Token, id, "raw/path", Fixture.Now).Value!;
        Assert.That(j.Stage, Is.EqualTo(JobStage.Map));
        Assert.That(j.ModelId, Is.EqualTo("model"));
        Assert.That(j.Operations[0].RawResultPath, Is.EqualTo("raw/path"));
        j = DocAiServices.JobLifecycleService.Retry(j, Fixture.Token, Fixture.Now).Value!;
        j = DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, j.NextAttemptAtUtc).Value!;
        Assert.That(DocAiServices.JobLifecycleService.Resume(j), Is.EqualTo(ResumeAction.Map));
        Assert.That(DocAiServices.JobLifecycleService.BeginOperation(j, Fixture.Token, Fixture.NextOperationId(), OperationKind.Extract, "m", Fixture.Now).IsSuccess, Is.False);
        j = DocAiServices.JobLifecycleService.Complete(j, Fixture.Token, Fixture.Now).Value!;
        Assert.That(j.Status, Is.EqualTo(JobStatus.Completed));
        Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Completed));
        Assert.That(j.Error, Is.Null);
        Assert.That(j.LeaseToken, Is.Null);
        Assert.That(j.CompletedAtUtc, Is.EqualTo(Fixture.Now));
        Assert.That(DocAiServices.JobLifecycleService.Complete(j, Fixture.Token, Fixture.Now).IsSuccess, Is.False);
    }

    [TestCase(ErrorCodes.AnalysisResultMissing)]
    [TestCase(ErrorCodes.AnalysisFailed)]
    public void Fatal_poll_closes_operation_with_job_and_run(string code)
    {
        var j = DocAiServices.JobLifecycleService.Fail(Fixture.Accepted(Fixture.Processing(), out var id), Fixture.Token, new(code, new string ('a', 401)), Fixture.Now, operationId: id).Value!;
        Assert.That(j.Status, Is.EqualTo(JobStatus.Failed));
        Assert.That(j.CurrentRun.Status, Is.EqualTo(RunStatus.Failed));
        Assert.That(j.Operations[0].State, Is.EqualTo(AzureOperationState.Failed));
        Assert.That(j.Attempts, Is.EqualTo(0));
        Assert.That(j.CurrentRun.Error!.Message!.Length, Is.EqualTo(400));
        Assert.That(j.Error!.Code, Is.EqualTo(code));
        Assert.That(DocAiServices.JobLifecycleService.Claim(j, Guid.NewGuid(), Fixture.Now.AddHours(1)).IsSuccess, Is.False);
    }

    [Test]
    public void New_runs_only_for_the_correct_terminal_status_and_restore_requested_line()
    {
        var active = Fixture.Processing(true);
        var classified = DocAiServices.JobLifecycleService.ClassifierSucceeded(Fixture.Accepted(active, out var id), Fixture.Token, id, "MAERSK", 1, .8, Fixture.Now).Value!;
        var failed = DocAiServices.JobLifecycleService.Fail(classified, Fixture.Token, new(ErrorCodes.InvalidDocument, "bad"), Fixture.Now).Value!;
        var completed = DocAiServices.JobLifecycleService.Complete(Fixture.Mapping(), Fixture.Token, Fixture.Now).Value!;
        foreach (var j in new[]
        {
            DocAiServices.JobLifecycleService.Create(null, true, Fixture.Now),
            active,
            failed,
            completed
        }

        )
            foreach (var reprocessRequested in new[]
            {
                false,
                true
            }

            )
            {
                var expected = j.Status == JobStatus.Failed || j.Status == JobStatus.Completed && reprocessRequested;
                var result = DocAiServices.JobLifecycleService.StartNewRun(j, reprocessRequested, false, Fixture.Now);
                Assert.That(result.IsSuccess, Is.EqualTo(expected));
                if (!expected)
                    continue;
                var next = result.Value!;
                Assert.That(next.CurrentRun.RunNo, Is.EqualTo(2));
                Assert.That(next.CurrentRun.Status, Is.EqualTo(RunStatus.Active));
                Assert.That(next.CurrentRun.Reason, Is.EqualTo(j.Status == JobStatus.Failed ? RunReason.Requeue : RunReason.Reprocess));
                Assert.That(next.Status, Is.EqualTo(JobStatus.Queued));
                Assert.That(next.Stage, Is.EqualTo(JobStage.Classify));
                Assert.That(next.ShippingLineCode, Is.Null);
                Assert.That(next.Attempts, Is.EqualTo(0));
                Assert.That(next.TakeoverCount, Is.EqualTo(0));
                Assert.That(next.Error, Is.Null);
                Assert.That(next.ModelId, Is.Null);
                Assert.That(next.Operations, Is.Empty);
                Assert.That(j.CurrentRun.Status, Is.EqualTo(j.Status == JobStatus.Failed ? RunStatus.Failed : RunStatus.Completed));
            }

        var run2 = DocAiServices.JobLifecycleService.StartNewRun(failed, false, true, Fixture.Now).Value!;
        Assert.That(run2.Stage, Is.EqualTo(JobStage.Extract));
        var run3 = DocAiServices.JobLifecycleService.StartNewRun(DocAiServices.JobLifecycleService.Fail(DocAiServices.JobLifecycleService.Claim(run2, Fixture.Token, Fixture.Now).Value!, Fixture.Token, new(ErrorCodes.InternalError, "x"), Fixture.Now).Value!, false, true, Fixture.Now).Value!;
        Assert.That(run3.CurrentRun.RunNo, Is.EqualTo(3));
    }

    [Test]
    public void Model_changes_only_restart_completed_jobs_and_explicit_reprocess_keeps_precedence()
    {
        var done = DocAiServices.JobLifecycleService.Complete(Fixture.Mapping(), Fixture.Token, Fixture.Now).Value!;
        Assert.That(DocAiServices.JobLifecycleService.StartNewRun(done, false, true, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.StartNewRun(done, false, true, Fixture.Now, modelChanged: true).Value!.CurrentRun.Reason, Is.EqualTo(RunReason.ModelChanged));
        Assert.That(DocAiServices.JobLifecycleService.StartNewRun(done, true, true, Fixture.Now, modelChanged: true).Value!.CurrentRun.Reason, Is.EqualTo(RunReason.Reprocess));
        var active = Fixture.Processing();
        Assert.That(DocAiServices.JobLifecycleService.StartNewRun(active, false, true, Fixture.Now, modelChanged: true).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.StartNewRun(DocAiServices.JobLifecycleService.Create(null, true, Fixture.Now), false, true, Fixture.Now, modelChanged: true).IsSuccess, Is.False);
        var failed = DocAiServices.JobLifecycleService.Fail(active, Fixture.Token, new(ErrorCodes.InvalidDocument, "bad"), Fixture.Now).Value!;
        Assert.That(DocAiServices.JobLifecycleService.StartNewRun(failed, false, true, Fixture.Now, modelChanged: true).Value!.CurrentRun.Reason, Is.EqualTo(RunReason.Requeue));
    }

    [Test]
    public void Invalid_operation_inputs_and_stages_are_rejected()
    {
        var j = Fixture.Processing();
        Assert.That(DocAiServices.JobLifecycleService.BeginOperation(j, Fixture.Token, Fixture.NextOperationId(), OperationKind.Classify, "m", Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.BeginOperation(j, Fixture.Token, 0, OperationKind.Extract, "m", Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.BeginOperation(j, Fixture.Token, Fixture.NextOperationId(), OperationKind.Extract, "", Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.RecordAccepted(j, Fixture.NextOperationId(), "location", Fixture.Now).IsSuccess, Is.False);
        var sending = Fixture.Operation(AzureOperationState.Sending);
        Assert.That(DocAiServices.AzureOperationLifecycleService.Accept(sending, "", Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.AzureOperationLifecycleService.Transition(sending, AzureOperationState.Accepted, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.AzureOperationLifecycleService.Start(sending.OperationId, 1, sending.Kind, "m", [sending], Fixture.Now).IsSuccess, Is.False);
    }
}
