using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class BoundaryTests
{
    [Test]
    public void Malformed_mapping_inputs_are_results_not_exceptions()
    {
        var valid = new ExtractedDocument("BL", null, "m", 1, ImmutableDictionary<string, ExtractedField?>.Empty, []);
        ExtractedDocument? [] invalid = [null, valid with
        {
            Fields = null!
        }, valid with
        {
            Containers = default
        }, valid with
        {
            DocumentTypeCode = ""
        }, valid with
        {
            ModelId = ""
        }, valid with
        {
            PageCount = 0
        }, valid with
        {
            Fields = valid.Fields.Add("", new("x", null, 1, 1))
        }, valid with
        {
            Containers = [null!]
        }, valid with
        {
            Containers = [new(null!)]
        }

        ];
        foreach (var d in invalid)
            Assert.That(DocAiServices.CanonicalResultMapperService.Map(d, .8).Rejection, Is.EqualTo(Rejection.InvalidInput));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(32768)]
    public void Invalid_operation_run_numbers_rejected(int run) => Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), run, OperationKind.Extract, "m", [], Fixture.Now).Rejection, Is.EqualTo(Rejection.InvalidInput));
    [Test]
    public void Operation_missing_history_and_unknown_kind_rejected()
    {
        Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 1, OperationKind.Extract, "m", null!, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.AzureOperationLifecycleService.Start(Fixture.NextOperationId(), 1, (OperationKind)99, "m", [], Fixture.Now).IsSuccess, Is.False);
    }

    [TestCase(-.1, .8)]
    [TestCase(1.1, .8)]
    [TestCase(double.NaN, .8)]
    [TestCase(.8, double.PositiveInfinity)]
    [TestCase(.8, -1)]
    public void Classifier_invalid_confidence_does_not_change_state(double confidence, double threshold)
    {
        var j = Fixture.Accepted(Fixture.Processing(true), out var id);
        Assert.That(DocAiServices.JobLifecycleService.ClassifierSucceeded(j, Fixture.Token, id, "MAERSK", confidence, threshold, Fixture.Now).Rejection, Is.EqualTo(Rejection.InvalidInput));
        Assert.That(j.Operations[0].State, Is.EqualTo(AzureOperationState.Accepted));
        Assert.That(j.Stage, Is.EqualTo(JobStage.Classify));
    }

    [Test]
    public void Stage_changes_require_an_accepted_operation_of_the_right_kind()
    {
        var id = Fixture.NextOperationId();
        var classify = DocAiServices.JobLifecycleService.BeginOperation(Fixture.Processing(true), Fixture.Token, id, OperationKind.Classify, "m", Fixture.Now).Value!;
        Assert.That(DocAiServices.JobLifecycleService.ClassifierSucceeded(classify, Fixture.Token, id, "line", 1, .8, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.ExtractionSucceeded(classify, Fixture.Token, id, "raw", Fixture.Now).IsSuccess, Is.False);
        var extract = DocAiServices.JobLifecycleService.BeginOperation(Fixture.Processing(), Fixture.Token, id, OperationKind.Extract, "m", Fixture.Now).Value!;
        Assert.That(DocAiServices.JobLifecycleService.ExtractionSucceeded(extract, Fixture.Token, id, "raw", Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.CloseSending(extract, Fixture.Token, id, AzureOperationState.Succeeded, Fixture.Now).IsSuccess, Is.False);
        Assert.That(DocAiServices.JobLifecycleService.Complete(extract, Fixture.Token, Fixture.Now).IsSuccess, Is.False);
    }

    [Test]
    public void Requested_line_is_restored_after_new_run()
    {
        var j = DocAiServices.JobLifecycleService.Fail(DocAiServices.JobLifecycleService.Claim(DocAiServices.JobLifecycleService.Create("MAERSK", false, Fixture.Now), Fixture.Token, Fixture.Now).Value!, Fixture.Token, new(ErrorCodes.ModelNotFound, "no model"), Fixture.Now).Value!;
        var next = DocAiServices.JobLifecycleService.StartNewRun(j, false, false, Fixture.Now).Value!;
        Assert.That(next.ShippingLineCode, Is.EqualTo("MAERSK"));
        Assert.That(next.Stage, Is.EqualTo(JobStage.Extract));
    }

    [Test]
    public void Run_number_does_not_wrap_SQL_smallint()
    {
        var j = DocAiServices.JobLifecycleService.Create(null, true, Fixture.Now);
        for (var n = 1; n < short.MaxValue; n++)
        {
            j = DocAiServices.JobLifecycleService.StartNewRun(DocAiServices.JobLifecycleService.Fail(DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, Fixture.Now).Value!, Fixture.Token, new(ErrorCodes.InternalError, "x"), Fixture.Now).Value!, false, true, Fixture.Now).Value!;
        }

        Assert.That(j.CurrentRun.RunNo, Is.EqualTo(short.MaxValue));
        j = DocAiServices.JobLifecycleService.Fail(DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, Fixture.Now).Value!, Fixture.Token, new(ErrorCodes.InternalError, "x"), Fixture.Now).Value!;
        Assert.That(DocAiServices.JobLifecycleService.StartNewRun(j, false, true, Fixture.Now).Rejection, Is.EqualTo(Rejection.RunLimitReached));
    }

    [Test]
    public void Retry_and_release_keep_Map_and_its_raw_result()
    {
        var j = Fixture.Mapping();
        j = DocAiServices.JobLifecycleService.Release(j, Fixture.Token, Fixture.Now, TimeSpan.FromMinutes(6)).Value!;
        Assert.That(j.Stage, Is.EqualTo(JobStage.Map));
        Assert.That(j.Operations[0].RawResultPath, Is.EqualTo("raw/path"));
        j = DocAiServices.JobLifecycleService.Claim(j, Fixture.Token, j.NextAttemptAtUtc).Value!;
        Assert.That(DocAiServices.JobLifecycleService.Resume(j), Is.EqualTo(ResumeAction.Map));
    }
}
