using ISTDocAi.Domain.Common.Constants.DocAi;
using NUnit.Framework;
using ISTDocAi.Application.Tests.DocAi.Services.Support;
using ISTDocAi.Domain.Common.Shared.DocAi.Snapshots;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Tests.DocAi.Services;
public sealed class PolicyTests
{
    [TestCase(0, 30)]
    [TestCase(1, 60)]
    [TestCase(2, 120)]
    [TestCase(3, 240)]
    public void Retry_backoff(int prior, int seconds)
    {
        var r = DocAiServices.RetryPolicyService.Failure(prior).Value!;
        Assert.That(r.Action, Is.EqualTo(RetryAction.Requeue));
        Assert.That(r.Attempts, Is.EqualTo(prior + 1));
        Assert.That(r.Delay, Is.EqualTo(TimeSpan.FromSeconds(seconds)));
    }

    [TestCase(0, 10, 30)]
    [TestCase(0, 30, 30)]
    [TestCase(0, 45, 45)]
    [TestCase(3, 240, 240)]
    [TestCase(3, 300, 300)]
    [TestCase(3, 301, 301)]
    [TestCase(0, 3600, 3600)]
    public void Failure_RetryAfter_is_a_minimum_and_still_uses_an_attempt(int attempts, int after, int expected)
    {
        var d = DocAiServices.RetryPolicyService.Failure(attempts, TimeSpan.FromSeconds(after)).Value!;
        Assert.That(d.Delay, Is.EqualTo(TimeSpan.FromSeconds(expected)));
        Assert.That(d.Attempts, Is.EqualTo(attempts + 1));
        Assert.That(d.Action, Is.EqualTo(RetryAction.Requeue));
    }

    [Test]
    public void Fifth_failure_exhausts()
    {
        var r = DocAiServices.RetryPolicyService.Failure(4, TimeSpan.FromHours(2)).Value!;
        Assert.That(r.Action, Is.EqualTo(RetryAction.Fail));
        Assert.That(r.Attempts, Is.EqualTo(5));
        Assert.That(r.ErrorCode, Is.EqualTo(ErrorCodes.RetriesExhausted));
    }

    [TestCase(0, RetryAction.Wait)]
    [TestCase(300, RetryAction.Wait)]
    [TestCase(301, RetryAction.Release)]
    [TestCase(3600, RetryAction.Release)]
    public void Poll_wait_boundary(int seconds, RetryAction expected)
    {
        var r = DocAiServices.RetryPolicyService.PollWait(4, TimeSpan.FromSeconds(seconds)).Value!;
        Assert.That(r.Action, Is.EqualTo(expected));
        Assert.That(r.Attempts, Is.EqualTo(4));
        Assert.That(r.Delay, Is.EqualTo(TimeSpan.FromSeconds(seconds)));
        Assert.That(r.ErrorCode, Is.Null);
    }

    [TestCase(-1)]
    [TestCase(5)]
    [TestCase(100)]
    public void Invalid_attempts_are_explicitly_rejected(int attempts)
    {
        Assert.That(DocAiServices.RetryPolicyService.Failure(attempts).Rejection, Is.EqualTo(Rejection.InvalidInput));
        Assert.That(DocAiServices.RetryPolicyService.PollWait(attempts, TimeSpan.Zero).Rejection, Is.EqualTo(Rejection.InvalidInput));
    }

    [Test]
    public void Negative_delays_rejected()
    {
        Assert.That(DocAiServices.RetryPolicyService.Failure(0, TimeSpan.FromSeconds(-1)).IsSuccess, Is.False);
        Assert.That(DocAiServices.RetryPolicyService.PollWait(0, TimeSpan.FromSeconds(-1)).IsSuccess, Is.False);
    }

    [Test]
    public void Configuration_and_lost_lease_consume_no_attempt()
    {
        var c = DocAiServices.RetryPolicyService.Configuration(4);
        Assert.That(c.Attempts, Is.EqualTo(4));
        Assert.That(c.Delay, Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(c.Action, Is.EqualTo(RetryAction.Release));
        Assert.That(c.ErrorCode, Is.EqualTo(ErrorCodes.AzureAuth));
        var l = DocAiServices.RetryPolicyService.LostLease(4);
        Assert.That(l.Attempts, Is.EqualTo(4));
        Assert.That(l.Action, Is.EqualTo(RetryAction.Stop));
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public void Takeover_budget(int count, bool fail) => Assert.That(DocAiServices.TakeoverPolicyService.IsExhausted(count), Is.EqualTo(fail));
    [TestCase(ErrorSource.PostAnalyze, 400, ErrorKind.Fatal, ErrorCodes.InvalidDocument)]
    [TestCase(ErrorSource.PostAnalyze, 404, ErrorKind.Fatal, ErrorCodes.ModelNotFound)]
    [TestCase(ErrorSource.GetPoll, 404, ErrorKind.Fatal, ErrorCodes.AnalysisResultMissing)]
    [TestCase(ErrorSource.PostAnalyze, 401, ErrorKind.Configuration, ErrorCodes.AzureAuth)]
    [TestCase(ErrorSource.GetPoll, 403, ErrorKind.Configuration, ErrorCodes.AzureAuth)]
    [TestCase(ErrorSource.PostAnalyze, 429, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.PostAnalyze, 408, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.GetPoll, 408, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.GetPoll, 500, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.GetPoll, 599, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.PostAnalyze, 503, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.Timeout, null, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.Mapping, null, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.Deadlock, null, ErrorKind.Retryable, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.LeaseLost, null, ErrorKind.LostLease, ErrorCodes.InternalError)]
    [TestCase(ErrorSource.AnalysisFailed, null, ErrorKind.Fatal, ErrorCodes.AnalysisFailed)]
    [TestCase(ErrorSource.OriginalMissing, null, ErrorKind.Fatal, ErrorCodes.OriginalMissing)]
    [TestCase(ErrorSource.Unclassified, null, ErrorKind.Fatal, ErrorCodes.Unclassified)]
    [TestCase(ErrorSource.ModelMissing, null, ErrorKind.Fatal, ErrorCodes.ModelNotFound)]
    [TestCase(ErrorSource.Configuration, null, ErrorKind.Configuration, ErrorCodes.AzureAuth)]
    [TestCase(ErrorSource.PostAnalyze, 418, ErrorKind.Fatal, ErrorCodes.InvalidDocument)]
    public void Error_classification(ErrorSource source, int? http, ErrorKind expected, string code)
    {
        var e = DocAiServices.ErrorClassifierService.Classify(source, http, new string ('x', 401));
        Assert.That(e.Kind, Is.EqualTo(expected));
        Assert.That(e.Error.Code, Is.EqualTo(code));
        Assert.That(e.Error.Message!.Length, Is.EqualTo(400));
        Assert.That(e.PauseWorker, Is.EqualTo(expected == ErrorKind.Configuration));
    }

    [TestCase(0)]
    [TestCase(399)]
    [TestCase(400)]
    [TestCase(401)]
    [TestCase(1000)]
    public void Error_message_is_bounded(int length) => Assert.That(new DomainError(ErrorCodes.InternalError, new string ('x', length)).Message!.Length, Is.EqualTo(Math.Min(length, 400)));
    [Test]
    public void Null_error_message_is_preserved() => Assert.That(new DomainError(ErrorCodes.InternalError, null).Message, Is.Null);
    [Test]
    public void Public_domain_codes_exist_in_contracts_and_queue_diagnostics_stay_internal()
    {
        var contract = typeof(ISTDocAi.Application.Common.Contracts.DocAi.V1.ErrorCodes).GetFields().Select(f => f.GetRawConstantValue()).ToHashSet();
        var domain = typeof(ErrorCodes).GetFields();
        Assert.That(domain, Is.Not.Empty);
        string[] queueOnly = [ErrorCodes.AzureThrottled, ErrorCodes.AzureUnavailable, ErrorCodes.MappingFailed, ErrorCodes.StorageUnavailable];
        foreach (var f in domain)
        {
            var code = (string)f.GetRawConstantValue()!;
            if (queueOnly.Contains(code))
                Assert.That(contract, Does.Not.Contain(code));
            else
                Assert.That(contract, Does.Contain(code));
        }
    }

    [TestCase(0, LeaseWriteResult.LeaseLost)]
    [TestCase(1, LeaseWriteResult.Applied)]
    public void Zero_rows_means_stop(int rows, LeaseWriteResult expected) => Assert.That(DocAiServices.LeasePolicyService.FromRowsChanged(rows), Is.EqualTo(expected));
    [TestCase(JobStatus.Queued)]
    [TestCase(JobStatus.Completed)]
    [TestCase(JobStatus.Failed)]
    public void Token_alone_does_not_allow_nonprocessing_writes(JobStatus status) => Assert.That(DocAiServices.LeasePolicyService.Check(status, Fixture.Token, Fixture.Token), Is.EqualTo(LeaseWriteResult.LeaseLost));
    [Test]
    public void Lease_requires_matching_nonempty_token()
    {
        Assert.That(DocAiServices.LeasePolicyService.Check(JobStatus.Processing, null, Guid.Empty), Is.EqualTo(LeaseWriteResult.LeaseLost));
        Assert.That(DocAiServices.LeasePolicyService.Check(JobStatus.Processing, Fixture.Token, Guid.NewGuid()), Is.EqualTo(LeaseWriteResult.LeaseLost));
        Assert.That(DocAiServices.LeasePolicyService.Check(JobStatus.Processing, Fixture.Token, Fixture.Token), Is.EqualTo(LeaseWriteResult.Applied));
    }
}
