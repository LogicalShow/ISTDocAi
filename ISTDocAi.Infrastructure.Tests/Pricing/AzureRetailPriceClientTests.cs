using System.Net;
using System.Text.Json;
using ISTDocAi.Infrastructure.Services.DocAi.Pricing;
using ISTDocAi.Application.Services.DocAi.Pricing;
using ISTDocAi.Infrastructure.Tests.DocAi.Worker;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.Pricing;
public sealed class AzureRetailPriceClientTests
{
    private sealed class Handler(Func<HttpRequestMessage,int,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) => send(request,Interlocked.Increment(ref Calls),ct);
    }
    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) { Assert.That(name,Is.EqualTo("AzureRetailPrices")); return new(handler,false); }
    }
    private static HttpResponseMessage Page(string json, HttpStatusCode status=HttpStatusCode.OK) => new(status) { Content=new StringContent(json) };
    private static string Json(string? next=null) => JsonSerializer.Serialize(new { Items=new[] { new { meterId=AzurePricePolicy.ExtractId,meterName="S0 Custom Pages",currencyCode="USD",armRegionName="francecentral",unitOfMeasure="1K",type="Consumption",tierMinimumUnits=0,retailPrice=30,effectiveStartDate="2024-06-01T00:00:00Z",productName="Azure Document Intelligence",skuName="S0",serviceName="Foundry Tools" } },NextPageLink=next });
    [Test]
    public async Task Valid_paging_preserves_duplicates_until_policy_and_sends_encoded_anonymous_query()
    {
        using var h=new Handler((r,n,_)=> {
            Assert.That(r.Headers.Authorization,Is.Null);
            Assert.That(r.RequestUri!.OriginalString,Does.Contain("%27"));
            Assert.That(r.RequestUri.AbsolutePath,Is.EqualTo("/api/retail/prices"));
            return Task.FromResult(Page(Json(n==1?AzureRetailPriceClient.InitialUri.AbsoluteUri+"&$skip=1":null)));
        });
        var rows=await new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default);
        Assert.That(rows,Has.Count.EqualTo(2));
        Assert.That(AzurePricePolicy.Evaluate(rows,AzurePricePolicyTests.Now)[0].ReasonCode,Is.EqualTo("METER_AMBIGUOUS"));
    }
    [TestCase("http://prices.azure.com/api/retail/prices")][TestCase("https://evil.example/api/retail/prices")]
    [TestCase("https://prices.azure.com:444/api/retail/prices")][TestCase("https://user@prices.azure.com/api/retail/prices")]
    [TestCase("https://prices.azure.com/api/retail/prices#x")][TestCase("https://prices.azure.com/other")]
    public void Hostile_links_are_rejected(string link) => Assert.Throws<InvalidDataException>(()=>AzureRetailPriceClient.ValidateLink(link));
    [TestCase("&evil=1")][TestCase("&$filter=x")][TestCase("&$skip=-1")]
    public void Changed_queries_are_rejected(string suffix) => Assert.Throws<InvalidDataException>(()=>AzureRetailPriceClient.ValidateLink(AzureRetailPriceClient.InitialUri.AbsoluteUri+suffix));
    [TestCase(302)][TestCase(400)][TestCase(401)]
    public void Redirects_and_business_refusals_are_not_retried(int status)
    {
        using var h=new Handler((_,_,_)=>Task.FromResult(Page("{}",(HttpStatusCode)status)));
        Assert.ThrowsAsync<InvalidDataException>(()=>new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default));
        Assert.That(h.Calls,Is.EqualTo(1));
    }
    [TestCase("{")][TestCase("{}")][TestCase("{\"Items\":null}")]
    public void Malformed_pages_are_not_retried(string json)
    {
        using var h=new Handler((_,_,_)=>Task.FromResult(Page(json)));
        Assert.CatchAsync<Exception>(()=>new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default));
        Assert.That(h.Calls,Is.EqualTo(1));
    }
    [Test]
    public void Repeated_pages_refuse()
    {
        using var h=new Handler((_,_,_)=>Task.FromResult(Page(Json(AzureRetailPriceClient.InitialUri.AbsoluteUri))));
        Assert.ThrowsAsync<InvalidDataException>(()=>new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default));
        Assert.That(h.Calls,Is.EqualTo(1));
    }
    [Test]
    public void Page_cap_refuses()
    {
        using var h=new Handler((_,n,_)=>Task.FromResult(Page(Json(AzureRetailPriceClient.InitialUri.AbsoluteUri+"&$skip="+n))));
        Assert.ThrowsAsync<InvalidDataException>(()=>new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default));
        Assert.That(h.Calls,Is.EqualTo(10));
    }
    [Test]
    public void Byte_cap_refuses()
    {
        using var h=new Handler((_,_,_)=>Task.FromResult(Page(new string(' ',1024*1024+1))));
        Assert.ThrowsAsync<InvalidDataException>(()=>new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default));
    }
    [Test]
    public async Task Retry_after_is_honoured_and_retries_are_bounded()
    {
        var clock=new TestClock();
        using var h=new Handler((_,_,_)=> { var response=Page("{}",HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new(TimeSpan.FromSeconds(7));return Task.FromResult(response); });
        var task=new AzureRetailPriceClient(new Factory(h),clock).ReadAsync(default);
        for(var i=0;i<2;i++) { await Until(()=>clock.Delays.Count(x=>x==TimeSpan.FromSeconds(7))>i);clock.Advance(TimeSpan.FromSeconds(7)); }
        try { await task; Assert.Fail("Expected refusal"); } catch(InvalidDataException) { }
        Assert.That(h.Calls,Is.EqualTo(3));
    }
    [Test]
    public async Task Shutdown_cancellation_aborts_transport()
    {
        using var h=new Handler(async (_,_,ct)=> { await Task.Delay(Timeout.Infinite,ct);return Page("{}"); });
        using var cancel=new CancellationTokenSource();
        var task=new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(cancel.Token);cancel.Cancel();
        try { await task;Assert.Fail("Expected cancellation"); } catch(OperationCanceledException) { }
        Assert.That(h.Calls,Is.EqualTo(1));
    }
    private sealed class UnboundedLengthContent(string body) : HttpContent
    {
        private readonly byte[] bytes=System.Text.Encoding.UTF8.GetBytes(body);
        protected override bool TryComputeLength(out long length) { length=0;return false; }
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
    [Test]
    public void Streamed_byte_cap_is_enforced_without_content_length()
    {
        using var h=new Handler((_,_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new UnboundedLengthContent(new string(' ',1024*1024+1)) }));
        Assert.ThrowsAsync<InvalidDataException>(()=>new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default));
        Assert.That(h.Calls,Is.EqualTo(1));
    }
    [Test]
    public void Total_byte_cap_is_enforced_across_pages()
    {
        using var h=new Handler((_,n,_)=>Task.FromResult(Page(JsonSerializer.Serialize(new { Items=Array.Empty<object>(),Padding=new string('x',900000),NextPageLink=AzureRetailPriceClient.InitialUri.AbsoluteUri+"&$skip="+n }))));
        Assert.ThrowsAsync<InvalidDataException>(()=>new AzureRetailPriceClient(new Factory(h),TimeProvider.System).ReadAsync(default));
        Assert.That(h.Calls,Is.EqualTo(5));
    }
    [Test]
    public async Task Transport_errors_retry_only_twice_with_bounded_jitter()
    {
        var clock=new TestClock();using var h=new Handler((_,_,_)=>throw new HttpRequestException("transport"));
        var task=new AzureRetailPriceClient(new Factory(h),clock).ReadAsync(default);
        await Until(()=>clock.Delays.Any(d=>d>=TimeSpan.FromSeconds(2)&&d<=TimeSpan.FromSeconds(2.5)));clock.Advance(TimeSpan.FromSeconds(3));
        await Until(()=>clock.Delays.Any(d=>d>=TimeSpan.FromSeconds(5)&&d<=TimeSpan.FromSeconds(5.5)));clock.Advance(TimeSpan.FromSeconds(6));
        try { await task;Assert.Fail("Expected transport failure"); } catch(HttpRequestException) { }
        Assert.That(h.Calls,Is.EqualTo(3));
    }
    [Test]
    public async Task Request_timeout_and_total_budget_cancel_transport()
    {
        var clock=new TestClock();using var h=new Handler(async (_,_,ct)=> { await Task.Delay(Timeout.Infinite,ct);return Page("{}"); });
        var task=new AzureRetailPriceClient(new Factory(h),clock).ReadAsync(default);
        await Until(()=>h.Calls==1);clock.Advance(TimeSpan.FromSeconds(30));
        await Until(()=>clock.Delays.Any(d=>d>=TimeSpan.FromSeconds(2)&&d<=TimeSpan.FromSeconds(2.5)));
        clock.Advance(TimeSpan.FromSeconds(3));await Until(()=>h.Calls==2);
        clock.Advance(TimeSpan.FromSeconds(90));
        try { await task;Assert.Fail("Expected budget cancellation"); } catch(OperationCanceledException) { }
        Assert.That(h.Calls,Is.EqualTo(2));
    }
    internal static async Task Until(Func<bool> condition)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while(!condition()) await Task.Delay(5,timeout.Token);
    }
}
