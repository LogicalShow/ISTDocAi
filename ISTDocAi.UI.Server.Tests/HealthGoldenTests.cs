using ISTDocAi.Application.Common.Interfaces.DocAi.Analysis;
using ISTDocAi.Application.Common.Interfaces.DocAi.Secrets;
using System.Net;
using System.Text;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
namespace ISTDocAi.UI.Server.Tests;
[NonParallelizable]
public sealed class HealthGoldenTests
{
    [TestCase("live.json")]
    [TestCase("ready.json")]
    public async Task Capture_scenario_matches_ready_and_live_golden_bytes_and_headers(string file)
    {
        var previous = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", null);
        try
        {
            using var factory = new BackendFactory(false);
            using var pending = factory.WithWebHostBuilder(b => {
                b.UseSetting("DocAi:Identity:Mode", "ManagedIdentity");
                b.UseSetting("DocAi:DocumentIntelligence:Endpoint", "");
                b.UseSetting("ConnectionStrings:DefaultConnection", "Server=invalid.invalid;Database=BackendTestsNeverConnect;Integrated Security=True");
                b.UseSetting("DocAi:KeyVault:Url", "https://docai-test.vault.azure.net");
                b.UseSetting("Secrets:JwtSigningKey", "");
                b.UseSetting("Encryption:EmailPasswordKey", "");
                b.UseSetting("Bootstrap:AdminPassword", "");
                b.ConfigureTestServices(s => { s.RemoveAll<IDatabaseReadiness>(); var gate = new DatabaseReadiness(); gate.MarkReady(); s.AddSingleton<IDatabaseReadiness>(gate);
                    s.RemoveAll<ISqlConnectionFactory>(); s.AddSingleton<ISqlConnectionFactory>(new NoSql());
                    s.RemoveAll<IDocumentAnalyzer>(); s.AddSingleton<IDocumentAnalyzer>(new WorkerFakes());
                    s.RemoveAll<ISecretStore>(); s.AddSingleton<ISecretStore>(new FakeSecrets());
                    s.RemoveAll<IHttpClientFactory>(); s.AddSingleton<IHttpClientFactory>(new NoHttp()); });
            });
            using var client = pending.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
            using var golden = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", file)));
            var request = golden.RootElement.GetProperty("request");
            using var message = new HttpRequestMessage(HttpMethod.Get, request.GetProperty("path").GetString());
            foreach (var h in request.GetProperty("headers").EnumerateArray())
                message.Headers.Add(h.GetProperty("name").GetString()!, h.GetProperty("values").EnumerateArray().Select(v => v.GetString()!));
            using var response = await client.SendAsync(message);
            var expected = golden.RootElement.GetProperty("response");
            Assert.That(response.Content.Headers.ContentType?.ToString(), Is.EqualTo(expected.GetProperty("contentType").GetString()));
            Assert.That((int)response.StatusCode, Is.EqualTo(expected.GetProperty("status").GetInt32()));
            Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.EqualTo(Encoding.UTF8.GetBytes(expected.GetProperty("bodyText").GetString()!)));
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(h => !new[] { "Date", "Server" }.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var expectedHeaders = expected.GetProperty("headers").EnumerateArray().ToDictionary(
                h => h.GetProperty("name").GetString()!, h => h.GetProperty("values").EnumerateArray().Select(v => v.GetString()!).ToArray(), StringComparer.OrdinalIgnoreCase);
            Assert.That(headers.Keys, Is.EquivalentTo(expectedHeaders.Keys));
            foreach (var h in expectedHeaders) Assert.That(headers[h.Key], Is.EqualTo(h.Value));
            using var ready = await client.GetAsync("/health/ready");
            Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            using var health = await client.GetAsync("/health");
            Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(Environment.GetEnvironmentVariable("JWT_SECRET_KEY"), Is.Null);
        }
        finally { Environment.SetEnvironmentVariable("JWT_SECRET_KEY", previous); }
    }
    private sealed class NoSql : ISqlConnectionFactory
    {
        public Task<SqlConnection> OpenAsync(CancellationToken ct) => throw new InvalidOperationException("SQL prohibited in pure capture");
    }

    private sealed class NoHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new DenyHandler());
    }
    private sealed class DenyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct) => throw new InvalidOperationException("Network prohibited");
    }
    private sealed class FakeSecrets : ISecretStore
    {
        public Task<byte[]> GetSigningKeyAsync(string name, CancellationToken ct) => Task.FromResult(new byte[32]);
        public Task<string> GetCaCertificatePemAsync(string name, CancellationToken ct) => throw new NotSupportedException();
        public void InvalidateSigningKey(string name) { }
    }
}
