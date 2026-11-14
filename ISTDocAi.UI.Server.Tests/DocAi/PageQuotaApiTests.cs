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
using System.Text.Json;

namespace ISTDocAi.UI.Server.Tests.DocAi;

public sealed class PageQuotaApiTests
{
    [Test]
    public async Task Quota_error_has_402_fixed_problem_and_page_counts()
    {
        using var app=new ApiFactory();
        const string message="The page allowance is exhausted or insufficient for this document. Contact IST to renew.";
        app.Ports.IntakeError=new IntakeException(402,"QUOTA_EXHAUSTED",message,message,requiredPages:6,availablePages:4);
        var response=await app.Client().PostAsync("/api/v2/jobs",IntakeTests.Form());
        Assert.Equal(HttpStatusCode.PaymentRequired,response.StatusCode);
        Assert.Equal("application/json",response.Content.Headers.ContentType!.MediaType);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("QUOTA_EXHAUSTED",json.RootElement.GetProperty("Error").GetProperty("Code").GetString());
        Assert.Equal(message,json.RootElement.GetProperty("Error").GetProperty("Message").GetString());
        Assert.Equal(message,json.RootElement.GetProperty("Error").GetProperty("Message").GetString());
        Assert.Equal(6,json.RootElement.GetProperty("Extensions").GetProperty("RequiredPages").GetInt64());
        Assert.Equal(4,json.RootElement.GetProperty("Extensions").GetProperty("AvailablePages").GetInt64());
    }
    [Test]
    public async Task Skipped_refresh_marker_is_returned_on_post_only()
    {
        using var app=new ApiFactory();
        app.Ports.FirstOutcome=IntakeOutcome.Existing; app.Ports.RereadSkipped="QUOTA_EXHAUSTED";
        var response=await app.Client().PostAsync("/api/v2/jobs",IntakeTests.Form());
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("QUOTA_EXHAUSTED",json.RootElement.GetProperty("Value").GetProperty("RereadSkipped").GetString());
        Assert.Equal(0,app.Ports.Uploads);
        Assert.Null(typeof(JobResponse).GetProperty("RereadSkipped"));
    }
}
