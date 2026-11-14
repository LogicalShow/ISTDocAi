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
using System.Text;
using System.Text.Json;



namespace ISTDocAi.UI.Server.Tests.DocAi;

[Category("SqlIntegration")]
internal sealed class RetentionApiTests
{
    private ISTDocAi.Infrastructure.Tests.DocAi.Runtime.SqlDatabase db = null!;
    [SetUp] public async Task SetUp() { db = new(); await db.InitializeAsync(); }
    [TearDown] public async Task TearDown() { if (db is not null) await db.DisposeAsync(); }
    [Test]
    [Category("Sql")]
    public async Task S13_returns_HTTP_404_after_retention_soft_deletes_job()
    {
        var job = await db.NewClaim();
        await db.Store.FailAsync(new(job.JobId, job.LeaseToken, "FAILED", null, false, Guid.NewGuid(), "{}"), default);
        using var app = new ApiFactory { Repository = db.Store }; var client = app.Client();
        var path = $"/api/v2/jobs/{job.JobId}?runNo=1";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        await db.Execute("UPDATE dbo.Jobs SET CompletedAtUtc=DATEADD(month,-14,SYSUTCDATETIME())");
        await db.Store.RunAsync(default);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }
}
