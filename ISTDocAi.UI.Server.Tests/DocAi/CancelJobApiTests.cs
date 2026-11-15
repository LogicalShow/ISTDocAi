using System.Net;
using System.Text.Json;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests.DocAi;
public sealed class CancelJobApiTests
{
    [TestCase("Cancelled")]
    [TestCase("Cancelling")]
    [TestCase("Continuing")]
    [TestCase("Finished")]
    public async Task Outcomes_and_repeated_requests_use_standard_transport(string outcome)
    {
        using var app = new ApiFactory();
        app.Ports.CancelResult = new(app.Ports.JobId, 2, outcome);
        using var client = app.Client();
        for (var i=0;i<2;i++)
        {
            using var response = await client.PostAsync("/api/v2/jobs/cancel?requestId=%20request-1%20", null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var value = json.RootElement.GetProperty("Value");
            Assert.That(value.GetProperty("Outcome").GetString(), Is.EqualTo(outcome));
            Assert.That(value.GetProperty("JobId").GetGuid(), Is.EqualTo(app.Ports.JobId));
            Assert.That(value.GetProperty("RunNo").GetInt32(), Is.EqualTo(2));
        }
        Assert.That(app.Ports.CancelRequestId, Is.EqualTo("request-1"));
    }
    [TestCase("")]
    [TestCase("?requestId=")]
    [TestCase("?requestId=%20")]
    [TestCase("?requestId=x&requestId=y")]
    [TestCase("?requestId=x%01y")]
    public async Task Invalid_request_is_400(string query)
    {
        using var app = new ApiFactory(); using var client = app.Client();
        using var response = await client.PostAsync("/api/v2/jobs/cancel"+query,null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(app.Ports.CancelRequestId, Is.Null);
    }
    [Test]
    public Task Too_long_request_is_400() => Invalid_request_is_400("?requestId="+new string('x',101));
    [TestCase(null)]
    [TestCase("wrong-key")]
    public async Task Authentication_is_required(string? key)
    {
        using var app = new ApiFactory(); using var client = app.Client(key);
        using var response = await client.PostAsync("/api/v2/jobs/cancel?requestId=request-1",null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    [TestCase(false)]
    [TestCase(true)]
    public async Task Unknown_and_foreign_requests_match_get_not_found(bool foreign)
    {
        using var app = new ApiFactory();
        if (foreign) { app.Ports.RunConsumerId=2; app.Ports.CancelResult=new(app.Ports.JobId,1,"Cancelled"); }
        using var client = app.Client();
        using var response = await client.PostAsync("/api/v2/jobs/cancel?requestId=request-1",null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("JOB_NOT_FOUND"));
    }
}
