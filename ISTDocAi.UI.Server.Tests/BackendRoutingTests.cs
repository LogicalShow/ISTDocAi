using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ISTDocAi.UI.Server.Tests;

[TestFixture]
public class BackendRoutingTests
{
    [Test]
    public async Task Health_IsAnonymousJson()
    {
        using var factory = new BackendFactory(null);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo("healthy"));
        Assert.That(factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().Where(a => a.ControllerTypeInfo.Namespace != "ISTDocAi.UI.Server.Controllers.DocAi"), Is.Empty);
    }

    [TestCase("/api/x")]
    [TestCase("/health/x")]
    [TestCase("/x")]
    [TestCase("/documents/review/123")]
    [TestCase("/index.html")]
    [TestCase("/browser/index.html")]
    [TestCase("/app-config.json")]
    public async Task UnknownRoutes_NeverReturnHtml(string path)
    {
        using var factory = new BackendFactory(false);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.Not.EqualTo("text/html"));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("<html").IgnoreCase.And.Not.Contain("<app-root").IgnoreCase);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TemplateSwitch_ControlsControllersAndHub(bool enabled)
    {
        using var factory = new BackendFactory(enabled);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToArray();
        Assert.That(actions.Count(a => a.ControllerTypeInfo.Namespace != "ISTDocAi.UI.Server.Controllers.DocAi"), enabled ? Is.GreaterThan(0) : Is.Zero);
        Assert.That(actions.Count(a => a.ControllerTypeInfo.Namespace == "ISTDocAi.UI.Server.Controllers.DocAi"), Is.EqualTo(8));
        if (enabled)
        {
            var expected = typeof(Program).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t));
            Assert.That(actions.Select(a => a.ControllerTypeInfo.AsType()).Distinct(), Is.EquivalentTo(expected));
        }

        using var login = await client.PostAsync("/api/iam/auth/login", new StringContent("{", Encoding.UTF8, "application/json"));
        Assert.That(login.StatusCode, Is.EqualTo(enabled ? HttpStatusCode.BadRequest : HttpStatusCode.NotFound));
        foreach (var path in new[] { "/api/IAM/UserMenu", "/api/public/Lookup/GetList", "/api/Home/Get" })
        {
            using var response = await client.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound), path);
        }
        using var hub = await client.PostAsync("/realtimeHub/negotiate?negotiateVersion=1", null);
        Assert.That(hub.StatusCode, enabled ? Is.Not.EqualTo(HttpStatusCode.NotFound) : Is.EqualTo(HttpStatusCode.NotFound));
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        Assert.That(endpoints.Any(e => e.DisplayName?.Contains("Fallback", StringComparison.OrdinalIgnoreCase) == true), Is.False);
    }

    [TestCase(false, "Development", false)]
    [TestCase(true, "Development", true)]
    [TestCase(true, "Production", false)]
    public async Task Documentation_RequiresFlagAndDevelopment(bool enabled, string environment, bool available)
    {
        using var factory = new BackendFactory(enabled, environment);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        foreach (var path in new[] { "/openapi/v1.json", "/scalar/v1" })
        {
            using var response = await client.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(available ? HttpStatusCode.OK : HttpStatusCode.NotFound), path);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExistingStaticFiles_AreNotServedOrUsedAsFallback(bool enabled)
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "static-probe-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path.Combine(webRoot, "browser"));
        try
        {
            File.WriteAllText(Path.Combine(webRoot, "index.html"), "<html><app-root>static probe</app-root></html>");
            File.WriteAllText(Path.Combine(webRoot, "browser", "index.html"), "<html>SPA probe</html>");
            File.WriteAllText(Path.Combine(webRoot, "probe.txt"), "static probe");
            using var factory = new BackendFactory(enabled);
            using var configured = factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));
            using var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            foreach (var path in new[] { "/", "/index.html", "/browser/index.html", "/probe.txt", "/documents/review/123" })
            {
                using var response = await client.GetAsync(path);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), path);
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.Not.EqualTo("text/html"), path);
                Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty, path);
            }
        }
        finally
        {
            if (webRoot.StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(webRoot, recursive: true);
        }
    }
}
