using NUnit.Framework;
using System.Security.Cryptography;
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

public sealed class TopUpUsageApiTests
{
    
    [TestCase("/usage/topups")]
    [TestCase("/usage/topups/1")]
    [TestCase("/usage/topups/prelude/documents")]
    public async Task Authentication_and_no_store(string path)
    {
        using var app = new ApiFactory();
        var response = await app.Client(null).GetAsync("/api/v2" + path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(app.Ports.UsageConsumerId);
    }
    [Test]
    public async Task Consumer_isolation_foreign_ids_and_cursors_are_404()
    {
        using var app = new ApiFactory();
        app.Ports.OtherConsumers.Add(app.Ports.Consumer with { ConsumerId=2, Code="OTHER", ApiKeyHash1=SHA256.HashData("second-key"u8), ApiKeyHash2=null });
        foreach (var (key,id) in new[] { ("test-key-one",1), ("second-key",2) })
        {
            var client = app.Client(key);
            foreach (var path in new[] { "", $"/{id}", "/prelude" })
            {
                var response = await client.GetAsync("/api/v2/usage/topups" + path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal((short)id, app.Ports.UsageConsumerId);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("TimestampGrantBeforeChargeV1", json.RootElement.GetProperty("Value").GetProperty("OrderingPolicy").GetString());
                var text = json.RootElement.GetRawText();
                foreach (var forbidden in new[] { "operatorIdentity", "note", "requestId", "fileName", "azure" })
                    Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            }
            foreach (var path in new[] { $"/{3-id}", $"/{3-id}/documents", "/999", $"/{id}/documents?afterChargeId=999" })
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v2/usage/topups" + path)).StatusCode);
        }
    }
    
    [TestCase("/usage/topups")]
    [TestCase("/usage/topups/1")]
    [TestCase("/usage/topups/prelude/documents")]
    public async Task Customer_json_has_no_cost_or_meter_fields(string path)
    {
        using var app=new ApiFactory();
        var response=await app.Client().GetAsync("/api/v2"+path);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var json=await response.Content.ReadAsStringAsync();
        foreach(var forbidden in new[]{"azure","cost","meter","price","usd"})
            Assert.DoesNotContain(forbidden,json,StringComparison.OrdinalIgnoreCase);
    }
    
    [TestCase("1", 1)]
    [TestCase("prelude", 0)]
    public async Task Documents_default_and_bounds_are_forwarded(string id, long expected)
    {
        using var app = new ApiFactory();
        var client = app.Client();
        foreach (var (query, limit, after) in new[] { ("",100,(long?)null), ("?limit=1&afterChargeId=3",1,(long?)3), ("?limit=500",500,(long?)null) })
        {
            var response = await client.GetAsync($"/api/v2/usage/topups/{id}/documents{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal((short)1, app.Ports.UsageConsumerId);
            Assert.Equal(expected, app.Ports.TopUpGrantId);
            Assert.Equal(limit, app.Ports.DocumentLimit);
            Assert.Equal(after, app.Ports.DocumentAfter);
        }
    }
    [Test]
    public async Task Existing_rate_limiter_applies()
    {
        using var app = new ApiFactory { Tokens=1 };
        var client = app.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v2/usage/topups")).StatusCode);
        var response = await client.GetAsync("/api/v2/usage/topups/prelude/documents");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }
    
    [TestCase("/usage/topups", "?grantId=1")]
    [TestCase("/usage/topups", "?limit=1")]
    [TestCase("/usage/topups/1", "?limit=1")]
    [TestCase("/usage/topups/0", "")]
    [TestCase("/usage/topups/-1", "")]
    [TestCase("/usage/topups/+1", "")]
    [TestCase("/usage/topups/9223372036854775808", "")]
    [TestCase("/usage/topups/Prelude", "")]
    [TestCase("/usage/topups/abc", "")]
    [TestCase("/usage/topups/prelude/documents", "?limit=0")]
    [TestCase("/usage/topups/prelude/documents", "?limit=501")]
    [TestCase("/usage/topups/prelude/documents", "?limit=")]
    [TestCase("/usage/topups/prelude/documents", "?limit=1&limit=2")]
    [TestCase("/usage/topups/prelude/documents", "?limit=1,2")]
    [TestCase("/usage/topups/prelude/documents", "?afterChargeId=0")]
    [TestCase("/usage/topups/prelude/documents", "?afterChargeId=-1")]
    [TestCase("/usage/topups/prelude/documents", "?afterChargeId=")]
    [TestCase("/usage/topups/prelude/documents", "?afterChargeId=1&afterChargeId=2")]
    [TestCase("/usage/topups/prelude/documents", "?afterChargeId=9223372036854775808")]
    [TestCase("/usage/topups/prelude/documents", "?consumerId=2")]
    public async Task Invalid_input_never_calls_reader(string path, string query)
    {
        using var app = new ApiFactory();
        var response = await app.Client().GetAsync("/api/v2" + path + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(app.Ports.UsageConsumerId);
    }
}


