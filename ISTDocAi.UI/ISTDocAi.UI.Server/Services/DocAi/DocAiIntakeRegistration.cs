using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.Services.DocAi.Intake;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.RateLimiting.DocAi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
namespace ISTDocAi.UI.Server.Services.DocAi;
public static class DocAiIntakeRegistration
{
    public static IServiceCollection AddDocAiIntake(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DocAiApiKeyHandler>(DocAiApiKeyHandler.SchemeName, _ => { });
        services.AddSingleton<IntakeCache>();
        services.AddSingleton<IConsumerDirectory>(s => s.GetRequiredService<IntakeCache>());
        services.AddSingleton<ICatalogReader>(s => s.GetRequiredService<IntakeCache>());
        services.AddOptions<FileInspectionOptions>().Bind(config.GetSection("DocAi:FileInspection"))
            .Validate(o => o.TimeoutSeconds is > 0 and <= 30, "File inspection timeout must be between 1 and 30 seconds.");
        services.AddSingleton<IFileInspector, FileInspector>();
        services.AddScoped<SubmitJob>(); services.AddScoped<GetCatalog>(); services.AddScoped<GetJob>();
        services.Configure<DocAiRateOptions>(config.GetSection("DocAi:RateLimit"));
        services.AddSingleton<DocAiConsumerLimiter>();
        services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 20 * 1024 * 1024 + 64 * 1024; o.ValueLengthLimit = 1024; o.ValueCountLimit = 10; });
        return services;
    }
}
