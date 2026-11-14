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

public sealed class UsageApiTests
{
    [Test]
    public async Task Default_six_months_no_store_and_only_key_consumer_is_forwarded()
    {
        using var app = new ApiFactory();
        var response = await app.Client().GetAsync("/api/v2/usage?consumerId=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal((short)1, app.Ports.UsageConsumerId);
        var body = await response.Content.ReadAsStringAsync();
        
        using var json = JsonDocument.Parse(body);
        Assert.Equal(6, json.RootElement.GetProperty("Value").GetProperty("Months").GetArrayLength());
        Assert.Equal("Low", json.RootElement.GetProperty("Value").GetProperty("Status").GetString());
        foreach (var forbidden in new[] { "operatorIdentity", "note", "requestId", "price" })
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
    }
    
    [TestCase("0")] [TestCase("25")] [TestCase("abc")] [TestCase("")] [TestCase("1&months=2")]
    public async Task Invalid_months_use_problem_format(string months)
    {
        using var app = new ApiFactory();
        var response = await app.Client().GetAsync("/api/v2/usage?months=" + months);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Null(app.Ports.UsageConsumerId);
    }
    [Test]
    public async Task Key_is_required()
    {
        using var app = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client(null).GetAsync("/api/v2/usage")).StatusCode);
    }
    [Test]
    public async Task Two_keys_receive_only_their_own_consumer_usage()
    {
        using var app = new ApiFactory();
        app.Ports.OtherConsumers.Add(app.Ports.Consumer with {
            ConsumerId = 2, Code = "SECOND", ApiKeyHash1 = System.Security.Cryptography.SHA256.HashData("second-key"u8), ApiKeyHash2 = null });
        foreach (var (key, granted) in new[] { ("test-key-one", 100), ("second-key", 200) })
        {
            var response = await app.Client(key).GetAsync("/api/v2/usage?consumerId=999");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(granted, json.RootElement.GetProperty("Value").GetProperty("Granted").GetInt64());
        }
    }
}
