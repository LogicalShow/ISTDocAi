using ISTDocAi.Domain.Common.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ISTDocAi.UI.Server.Authorization;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permissionKey;

    public RequirePermissionAttribute(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        _permissionKey = permissionKey;
    }

    public RequirePermissionAttribute(PageDataEnum pageId, ActionDataEnum action)
        : this($"{pageId.GetStringValue()}.{action}")
    {
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = ApiAuthorizationFailureFactory
                .CreateUnauthorizedActionResult(context.HttpContext);
            return;
        }

        if (!user.HasClaim("permission", _permissionKey))
        {
            context.Result = ApiAuthorizationFailureFactory
                .CreateForbiddenActionResult(context.HttpContext);
        }
    }
}
