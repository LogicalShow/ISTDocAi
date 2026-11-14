using ISTDocAi.Application.Startup;
using ISTDocAi.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ISTDocAi.UI.Server.Tests;

internal class BackendFactory(bool? enabled, string environment = "Development") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // Host settings are supplied before Program reads configuration or registers Infrastructure.
        if (enabled.HasValue)
            builder.UseSetting("TemplateEndpoints:Enabled", enabled.Value.ToString());
        builder.UseSetting("Secrets:JwtSigningKey", "backend-tests-only-signing-key-at-least-32-characters");
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Server=invalid.invalid;Database=BackendTestsNeverConnect;Integrated Security=True");
        builder.ConfigureTestServices(services =>
        {
            var ready = new ISTDocAi.Application.Services.DocAi.DatabaseReadinessService.DatabaseReadiness();
            ready.MarkReady();
            services.AddSingleton<ISTDocAi.Application.Common.Interfaces.DocAi.IDatabaseReadiness>(ready);
            services.RemoveAll<IStartupTask>();
            services.RemoveAll<IHostedService>();
            // Any accidental database resolution fails immediately, before a connection can open.
            services.RemoveAll<AppDbContext>();
            services.AddScoped<AppDbContext>(_ => throw new InvalidOperationException("Database access is forbidden in server tests."));
        });
    }
}
