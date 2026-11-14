using NUnit.Framework;
using Assert = ISTDocAi.UI.Server.Tests.PortAssert;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;

using ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
namespace ISTDocAi.UI.Server.Tests.DocAi;

[Category("SqlIntegration")]
internal sealed class SqlIntakeTests
{
    private SqlDatabase database = null!;
    [SetUp] public async Task SetUp() { database = new SqlDatabase(); await database.InitializeAsync(); }
    [TearDown] public async Task TearDown() { if (database is not null) await database.DisposeAsync(); }
    [Test]
    [Category("Sql")]
    public async Task Consumer_limit_200_accepts_60_pages_and_rejects_201()
    {
        await database.Execute("UPDATE dbo.Consumers SET MaxPagesPerJob=200,DailyPageCap=1000 WHERE ConsumerId=1");
        using var app = new ApiFactory { Repository = database.Store };
        var consumers = await database.Store.GetConsumersAsync(default);
        app.Ports.Consumer = app.Ports.Consumer with { MaxPagesPerJob = consumers.Single().MaxPagesPerJob, DailyPageCap = 1000 };
        var client = app.Client();
        var rejected = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(IntakeTests.Pdf(201)));
        Assert.Equal((HttpStatusCode)422, rejected.StatusCode);
        Assert.Contains("TOO_MANY_PAGES", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(0, app.Ports.Uploads);
        var accepted = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(IntakeTests.Pdf(60)));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(60, (await accepted.ReadValue<SubmitJobResponse>())!.PageCount);
        Assert.Equal(1, app.Ports.Uploads);
    }

    [Test]
    [Category("Sql")]
    public async Task Same_file_posts_twice_bind_same_run_and_upload_once()
    {
        using var app = new ApiFactory { Repository = database.Store };
        var client = app.Client();
        var bytes = IntakeTests.Pdf();
        var first = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(bytes, client:"submission-one"));
        Assert.Equal(HttpStatusCode.Accepted,first.StatusCode);
        var created = (await first.ReadValue<SubmitJobResponse>())!;
        var second = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(bytes, client:"submission-two"));
        Assert.Equal(HttpStatusCode.OK,second.StatusCode);
        var existing = (await second.ReadValue<SubmitJobResponse>())!;
        Assert.Equal(created.JobId,existing.JobId);
        Assert.Equal(created.RunNo,existing.RunNo);
        Assert.Equal(1,app.Ports.Uploads);
        var third = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(bytes,client:"submission-one"));
        Assert.Equal(HttpStatusCode.OK,third.StatusCode);
        Assert.Equal(1,app.Ports.Uploads);
    }
}

[Category("SqlIntegration")]
internal sealed class ModelChangedApiSqlTests
{
    private SqlDatabase database = null!;
    [SetUp] public async Task SetUp() { database = new SqlDatabase(); await database.InitializeAsync(); }
    [TearDown] public async Task TearDown() { if (database is not null) await database.DisposeAsync(); }
    [Test]
    [Category("Sql")]
    public async Task Changed_completed_file_obeys_daily_cap_before_upload_or_new_run()
    {
        using var app = new ApiFactory { Repository = database.Store };
        var client = app.Client();
        var bytes = IntakeTests.Pdf();
        var response = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(bytes, client: "cap-original"));
        var submitted = (await response.ReadValue<SubmitJobResponse>())!;
        var job = (await SqlClaimTestHelper.ExpectedAsync(database.Store.ClaimAsync));
        var operation = await database.Accepted(job);
        await database.Store.ExtractionSucceededAsync(new(job.JobId, job.LeaseToken, operation, "bl-v1", 1,
            DateTimeOffset.UtcNow, "raw.json"), default);
        await database.Store.CompleteAsync(new(job.JobId, job.LeaseToken, "{}", 0, Guid.NewGuid(), "{}"), default);
        // NULL is stale. The first run consumes the cap, so the automatic rerun must be refused.
        await database.Execute("UPDATE dbo.Consumers SET DailyPageCap=1 WHERE ConsumerId=1");
        app.Ports.Consumer = app.Ports.Consumer with { DailyPageCap = 1 };
        app.Clock.Now = app.Clock.Now.AddSeconds(61);
        var limited = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(bytes, client: "cap-new"));
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
        Assert.Contains("DAILY_CAP_REACHED", await limited.Content.ReadAsStringAsync());
        Assert.Equal(1, app.Ports.Uploads);
        Assert.Equal(1, await database.Scalar<int>("SELECT COUNT(*) FROM dbo.JobRuns WHERE JobId=@id", new { id = submitted.JobId }));
        var replay = await client.PostAsync("/api/v2/jobs", IntakeTests.Form(bytes, client: "cap-original"));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, (await replay.ReadValue<SubmitJobResponse>())!.RunNo);
    }

}
