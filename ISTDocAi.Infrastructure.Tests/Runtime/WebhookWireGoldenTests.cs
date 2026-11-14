using ISTDocAi.Application.Services.DocAi.Webhooks;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Webhooks;
using ISTDocAi.Infrastructure.Services.DocAi.Webhooks;
using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
public sealed class WebhookWireGoldenTests
{
    [TestCase("bl")]
    [TestCase("manifest")]
    [TestCase("model-changed")]
    [TestCase("unclassified")]
    public async Task Frozen_payload_signature_and_all_transport_headers_match(string name)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Webhook", name + ".json")));
        var golden = fixture.RootElement;
        var headers = golden.GetProperty("headers").EnumerateArray().ToDictionary(h => h.GetProperty("name").GetString()!, h => h.GetProperty("values").EnumerateArray().Select(x => x.GetString()!).ToArray(), StringComparer.OrdinalIgnoreCase);
        var body = Convert.FromBase64String(golden.GetProperty("bodyBase64").GetString()!);
        Assert.That(Convert.ToHexStringLower(SHA256.HashData(body)), Is.EqualTo(golden.GetProperty("bodySha256").GetString()));
        Assert.That(body, Is.EqualTo(Encoding.UTF8.GetBytes(golden.GetProperty("bodyText").GetString()!)));
        var signature = WebhookSignature.Compute(Convert.FromBase64String(golden.GetProperty("fakeSecretBase64").GetString()!), headers["X-Timestamp"][0], body);
        Assert.That(signature, Is.EqualTo(headers["X-Signature"][0]));
        using var handler = new Capture(async message =>
        {
            Assert.That(message.Method.Method, Is.EqualTo(golden.GetProperty("method").GetString()));
            Assert.That(message.RequestUri!.AbsoluteUri, Is.EqualTo(golden.GetProperty("url").GetString()));
            Assert.That(await message.Content!.ReadAsByteArrayAsync(), Is.EqualTo(body));
            var actual = message.Headers.Concat(message.Content.Headers).ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            Assert.That(actual.Keys, Is.EquivalentTo(headers.Keys));
            foreach (var h in headers) Assert.That(actual[h.Key], Is.EqualTo(h.Value));
            Assert.That(message.Options.TryGetValue(new HttpRequestOptionsKey<bool>("DocAi.SensitiveUrl"), out var sensitive) && sensitive, Is.True);
            // A redirect is returned to delivery policy; the transport performs one request.
            return new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://must-not-follow.invalid") } };
        });
        using var transport = new WebhookHttpTransport(new Factory(handler));
        var response = await transport.SendAsync(new(new CallbackConsumer(1, "TEST", golden.GetProperty("url").GetString()!, "fake", null), body, headers["X-Timestamp"][0], signature, headers["X-Correlation-Id"][0], null), default);
        Assert.That(response.StatusCode, Is.EqualTo(302));
        Assert.That(handler.Calls, Is.EqualTo(1));
    }
    private sealed class Capture(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct) { Calls++; return send(message); }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
}
