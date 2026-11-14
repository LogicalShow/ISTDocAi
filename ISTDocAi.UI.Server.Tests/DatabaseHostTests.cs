using System.Net;
using System.Text;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi;
using ISTDocAi.Application.Services.DocAi.DatabaseReadinessService;
using ISTDocAi.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace ISTDocAi.UI.Server.Tests;

[NonParallelizable]
public sealed class DatabaseHostTests
{
    [TestCase("live.json")]
    [TestCase("not-ready-before-auth.json")]
    public async Task Pending_host_matches_http_golden_including_all_headers_and_body_bytes(string file)
    {
        var previous = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", null);
        try
        {
            using var factory = new BackendFactory(false);
            using var pending = factory.WithWebHostBuilder(b => {
                b.UseSetting("Secrets:JwtSigningKey", "");
                b.UseSetting("Encryption:EmailPasswordKey", "");
                b.UseSetting("Bootstrap:AdminPassword", "");
                b.ConfigureTestServices(s => { s.RemoveAll<IDatabaseReadiness>(); s.AddSingleton<IDatabaseReadiness>(new DatabaseReadiness()); });
            });
            using var client = pending.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
            using var golden = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", file)));
            var request = golden.RootElement.GetProperty("request");
            using var message = new HttpRequestMessage(HttpMethod.Get, request.GetProperty("path").GetString()!.Replace("/api/v1/", "/api/v2/", StringComparison.Ordinal));
            foreach (var h in request.GetProperty("headers").EnumerateArray())
                message.Headers.Add(h.GetProperty("name").GetString()!, h.GetProperty("values").EnumerateArray().Select(v => v.GetString()!));
            using var response = await client.SendAsync(message);
            var expected = golden.RootElement.GetProperty("response");
            Assert.That((int)response.StatusCode, Is.EqualTo(expected.GetProperty("status").GetInt32()));
            if (file == "live.json")
                Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.EqualTo(Encoding.UTF8.GetBytes(expected.GetProperty("bodyText").GetString()!)));
            else
            {
                using var oldBody = JsonDocument.Parse(expected.GetProperty("bodyText").GetString()!);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.That(body.RootElement.GetProperty("IsSuccess").GetBoolean(), Is.False);
                Assert.That(body.RootElement.GetProperty("Error").GetProperty("Code").GetString(), Is.EqualTo(oldBody.RootElement.GetProperty("code").GetString()));
                Assert.That(body.RootElement.GetProperty("Error").GetProperty("Message").GetString(), Is.EqualTo(oldBody.RootElement.GetProperty("detail").GetString()));
                Assert.That(body.RootElement.GetProperty("Extensions").GetProperty("CorrelationId").GetString(), Is.EqualTo(oldBody.RootElement.GetProperty("correlationId").GetString()));
            }
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(h => !new[] { "Date", "Server" }.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var expectedHeaders = expected.GetProperty("headers").EnumerateArray().ToDictionary(
                h => h.GetProperty("name").GetString()!, h => h.GetProperty("values").EnumerateArray().Select(v => v.GetString()!).ToArray(), StringComparer.OrdinalIgnoreCase);
            if (file != "live.json") expectedHeaders["Content-Type"] = ["application/json; charset=utf-8"];
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

    [Test]
    public void Enabled_template_still_requires_jwt()
    {
        var previous = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", null);
        try
        {
            var services = new ServiceCollection();
            Assert.That(() => services.AddInfrastructure("Server=invalid;Database=Test;Integrated Security=True", true),
                Throws.InvalidOperationException.With.Message.Contains("JWT_SECRET_KEY"));
        }
        finally { Environment.SetEnvironmentVariable("JWT_SECRET_KEY", previous); }
    }

    [Test]
    public async Task Readiness_is_one_way_and_wait_is_cancelable()
    {
        var gate = new DatabaseReadiness();
        Assert.That(gate.Status.State, Is.EqualTo(DatabaseReadinessState.Pending));
        using var ct = new CancellationTokenSource();
        var wait = gate.WaitUntilReadyAsync(ct.Token);
        Assert.That(wait.IsCompleted, Is.False);
        gate.MarkFailed("FIXED_FAILURE"); gate.MarkReady();
        Assert.That(gate.Status.State, Is.EqualTo(DatabaseReadinessState.Failed));
        ct.Cancel();
        Assert.That(async () => await wait, Throws.InstanceOf<OperationCanceledException>());
        var ready = new DatabaseReadiness(); ready.MarkReady(); ready.MarkFailed("IGNORED");
        await ready.WaitUntilReadyAsync(CancellationToken.None);
        Assert.That(ready.Status.State, Is.EqualTo(DatabaseReadinessState.Ready));
    }
}
