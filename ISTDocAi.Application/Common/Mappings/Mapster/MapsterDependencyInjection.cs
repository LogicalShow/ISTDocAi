using System.Reflection;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace ISTDocAi.Application.Common.Mappings.Mapster;

public static class MapsterDependencyInjection
{
    public static IServiceCollection AddMapsterMappings(
        this IServiceCollection services,
        Assembly assembly)
    {
        var config = new TypeAdapterConfig();
        config.Scan(assembly);
        config.Compile();

        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
