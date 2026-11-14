using ISTDocAi.Infrastructure.Serialization;
using Microsoft.AspNetCore.SignalR;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace ISTDocAi.UI.Server.Serialization;

public static class ApiJsonServiceCollectionExtensions
{
    public static IServiceCollection AddApiJsonSerialization(
        this IServiceCollection services)
    {
        services.Configure<MvcJsonOptions>(options =>
            SharedJsonConfiguration.Configure(
                options.JsonSerializerOptions));

        services.Configure<HttpJsonOptions>(options =>
            SharedJsonConfiguration.Configure(
                options.SerializerOptions));

        services.Configure<JsonHubProtocolOptions>(options =>
            SharedJsonConfiguration.Configure(
                options.PayloadSerializerOptions));

        return services;
    }
}
