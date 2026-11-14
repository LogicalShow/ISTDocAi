using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
namespace ISTDocAi.Infrastructure;
public static class DocAiMappingRegistration
{
    private sealed class SqlOnlyMappingStartupGuard { }

    public static IServiceCollection AddDocAiMapping(this IServiceCollection services)
    {
        services.AddOptions<SqlOnlyMappingStartupGuard>()
            .Validate<Microsoft.Extensions.Configuration.IConfiguration>((_, configuration) =>
            {
                var source = configuration["DocAi:Mapping:Source"];
                return string.IsNullOrEmpty(source) || source == "Sql";
            }, "Mapping profiles come from SQL only.").ValidateOnStart();
        services.TryAddSingleton<SqlMappingProfileResolver>();
        services.TryAddSingleton<MappingProfileReadiness>();
        services.TryAddSingleton<IMappingProfileResolver>(s => s.GetRequiredService<SqlMappingProfileResolver>());
        services.TryAddSingleton<IAnalysisParser>(s => new AnalysisParser(s.GetRequiredService<IMappingProfileResolver>()));
        services.TryAddSingleton(s => new ModelResultMapper(s.GetRequiredService<IMappingProfileResolver>()));
        return services;
    }
}
