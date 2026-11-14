using ISTDocAi.UI.Server.Services.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using ISTDocAi.UI.Server.Middleware.DocAi;
using ISTDocAi.Application;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Infrastructure;
using ISTDocAi.Infrastructure.Realtime;
using ISTDocAi.Infrastructure.IAM.Services;
using ISTDocAi.Infrastructure.Services;
using Quartz;
using Scalar.AspNetCore;
using Serilog;
using ISTDocAi.UI.Server;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Middleware;
using ISTDocAi.UI.Server.RateLimiting;
using ISTDocAi.UI.Server.Validation;
using ISTDocAi.UI.Server.Serialization;
using ISTDocAi.UI.Server.Services.Public.Attachment;
using ISTDocAi.UI.Server.Services.Public.Setting;
using System.Globalization;
using ISTDocAi.Domain.Common.Constants.Localization;
using Microsoft.AspNetCore.Localization;
using ISTDocAi.UI.Server.Configuration;



bool inSeedMode = args.Length > 0 && args[0] == "--seed";
if (inSeedMode) Console.WriteLine("Seeding database...");

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables().AddCommandLine(args);
// Local setup writes a unique signing key; externally supplied keys take precedence.
var templateEndpointsEnabled = builder.Configuration.GetValue<bool>("TemplateEndpoints:Enabled");
if (templateEndpointsEnabled && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_SECRET_KEY")))
    Environment.SetEnvironmentVariable("JWT_SECRET_KEY", builder.Configuration["Secrets:JwtSigningKey"]);

// Configure logging to suppress EF Core SQL logs
builder.Logging.Services.PostConfigure<Microsoft.Extensions.Logging.LoggerFilterOptions>(o =>
    o.Rules.Add(new Microsoft.Extensions.Logging.LoggerFilterRule(null, "Microsoft.EntityFrameworkCore", LogLevel.None, null)));

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Filter.ByExcluding(e => e.Properties.TryGetValue("SourceContext", out var value)
        && value is Serilog.Events.ScalarValue { Value: string source }
        && source.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
    .CreateLogger();

// Keep the standard LoggerFactory so OpenTelemetry and its filters remain active.
// Clear the template/default sinks, then add exactly one Serilog provider.
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);
builder.Services.AddSingleton(new Serilog.Extensions.Hosting.DiagnosticContext(Log.Logger));
builder.Services.AddSingleton<Serilog.IDiagnosticContext>(s => s.GetRequiredService<Serilog.Extensions.Hosting.DiagnosticContext>());
builder.Services.AddDocAiTelemetry(builder.Configuration);

// Detailed SignalR errors may expose server implementation details, so only
// return them while developing locally.
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
}).AddJsonProtocol();

// Add DependencyInjection: Application
builder.Services.AddApplication();

// Add DependencyInjection: Infrastructure
builder.Services.AddInfrastructure(DatabaseConfiguration.RuntimeConnection(builder.Configuration), templateEndpointsEnabled);
builder.Services.AddDocAiDatabase(builder.Configuration, inSeedMode);
builder.Services.AddDocAiAzure(builder.Environment, builder.Configuration);
builder.Services.AddDocAiWorker(builder.Configuration);

builder.Services.AddApiAuthorization();
builder.Services.AddDocAiIntake(builder.Configuration);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024 + 64 * 1024);

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = CultureConstants.Supported.Select(c => new CultureInfo(c)).ToList();
    options.DefaultRequestCulture = new RequestCulture(CultureConstants.Default);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
    options.RequestCultureProviders =
    [
        new CustomRequestCultureProvider(ctx =>
        {
            var header = ctx.Request.Headers["X-Culture"].FirstOrDefault();
            return Task.FromResult(string.IsNullOrWhiteSpace(header)
                ? null
                : new ProviderCultureResult(header));
        }),
        new QueryStringRequestCultureProvider { QueryStringKey = "culture", UIQueryStringKey = "culture" }
    ];
});

// Add Api Controllers
builder.Services.AddControllersWithViews(options =>
    {
        if (!templateEndpointsEnabled)
            options.Conventions.Add(new TemplateEndpointsConvention());
    })
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ApiValidationResponseFactory.Create);

builder.Services.AddApiJsonSerialization();

// Add OpenAPI
builder.Services.AddOpenApi();

builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<ISettingWebService, SettingWebService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddDistributedMemoryCache();

// Add Rate Limiting
builder.Services.AddAppRateLimiting(builder.Configuration);

// Report token lifetime - read via IOptionsMonitor, so edits apply without a restart.
builder.Services.Configure<ReportTokenOptions>(
    builder.Configuration.GetSection(ReportTokenOptions.SectionName));

// Register Startup Pipeline and Tasks
builder.Services.AddStartupPipeline();

// Add Quartz Configuration
if (!inSeedMode && templateEndpointsEnabled)
    builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

// Add Fast Report Registration

var app = builder.Build();

if (inSeedMode)
{
    Environment.ExitCode = await app.Services.GetRequiredService<UnifiedDatabaseInitializer>().InitializeAsync() ? 0 : 1;
    await app.DisposeAsync();
    return;
}

app.UseMiddleware<DocAiBoundaryMiddleware>();

// Add custom error handling middleware
app.UseMiddleware<DocAiApiMiddleware>();
app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api/v2"), branch => branch.UseMiddleware<ErrorHandlingMiddleware>());

app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api/v2"), branch => branch.UseHttpsRedirection());

app.UseRouting();
app.UseMiddleware<ISTDocAi.UI.Server.RateLimiting.DocAi.DocAiConsumerRateMiddleware>();

app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api/v2"), branch => branch.UseSerilogRequestLogging());

app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api/v2"), branch => branch.UseRequestLocalization());

// Add authentication middleware
app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api/v2"), branch => branch.UseAuthentication());
app.UseAuthorization();

// Rate limiting runs after authentication so each signed-in user gets their own
// bucket instead of everyone behind a proxy sharing one address-keyed bucket.
app.UseAppRateLimiting();

// Add Fat Report Viewer middleware

// Map SignalR hubs
if (templateEndpointsEnabled)
    app.MapHub<RealtimeHub>("/realtimeHub").DisableRateLimiting();

// Map controllers
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", application = "ISTDocAi" })).AllowAnonymous();

// Template documentation is available only in Development, when explicitly enabled.
if (templateEndpointsEnabled && app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();

public partial class Program;
