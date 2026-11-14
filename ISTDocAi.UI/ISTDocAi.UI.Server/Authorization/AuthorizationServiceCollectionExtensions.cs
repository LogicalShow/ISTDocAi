using Microsoft.AspNetCore.Authorization;

namespace ISTDocAi.UI.Server.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddApiAuthorization(
        this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler,
            ApiAuthorizationMiddlewareResultHandler>();

        return services;
    }
}
