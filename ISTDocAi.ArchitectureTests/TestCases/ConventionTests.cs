using FluentValidation;
using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.UI.Server.Controllers.IAM;
using ISTDocAi.UI.Server.Controllers.Public;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class ConventionTests
{
    [Test]
    public void EveryRequest_ShouldHaveValidator()
    {
        // Arrange
        var types = Assemblies.Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .ToList();

        // Requests without input (no public instance properties) have nothing to validate.
        var requests = types.Where(t => typeof(IBaseRequest).IsAssignableFrom(t)
            && t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0);

        var validatedTypes = types
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToHashSet();

        // Act
        var missing = requests
            .Where(r => !validatedTypes.Contains(r))
            .Select(r => r.FullName)
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.That(missing, Is.Empty, "Requests without validator:\n" + string.Join("\n", missing));
    }

    /// <summary>
    /// Endpoints that technically cannot carry a page permission.
    /// Keep this list short: every entry is an endpoint any caller (or any logged-in user) can hit.
    /// </summary>
    private static readonly (Type Controller, string Action)[] ExcludedEndpoints =
    [
        // Anonymous auth flow: caller has no token yet
        (typeof(AuthController), nameof(AuthController.Login)),
        (typeof(AuthController), nameof(AuthController.Logout)),
        (typeof(AuthController), nameof(AuthController.SendOtpEmail)),   // forgot password: send OTP
        (typeof(AuthController), nameof(AuthController.VerifyOtp)),      // forgot password: verify OTP
        (typeof(AuthController), nameof(AuthController.ResetPassword)),  // forgot password: set new password
        (typeof(AuthController), nameof(AuthController.PasswordPolicy)), // public Identity password policy

        // Shared shell endpoints: authenticated users, no page-specific data.
        (typeof(ISTDocAi.UI.Server.Controllers.HomeController), "Get"),
        (typeof(LookupController), nameof(LookupController.GetList)),

        // Current-user self-service: logged in, acts only on own account/data
        (typeof(AuthController), nameof(AuthController.SendOtpToCurrentUser)),
        (typeof(AuthController), nameof(AuthController.ChangeCurrentUserPassword)),
        (typeof(AuthController), nameof(AuthController.Permissions)),
        (typeof(AuthController), nameof(AuthController.ReportToken)),
        (typeof(UserMenuController), nameof(UserMenuController.Get)),
        (typeof(NotificationUserController), nameof(NotificationUserController.GetUserNotifications)),
        (typeof(NotificationUserController), nameof(NotificationUserController.GetUserNotificationsPaginated)),
        (typeof(NotificationUserController), nameof(NotificationUserController.AddUserToAssienedNotificationGroups)),
        (typeof(NotificationUserDetailController), nameof(NotificationUserDetailController.MarkAsRead)),
    ];

    [Test]
    public void EveryControllerAction_ShouldHavePermissionAttribute()
    {
        // Arrange
        var controllers = Assemblies.Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        // Act
        var missing = controllers
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !m.IsDefined(typeof(NonActionAttribute)))
                .Where(m => !HasPermission(c, m) && !IsExcluded(c, m) && !HasDocAiScheme(c))
                .Select(m => $"{c.FullName}.{m.Name}"))
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.That(missing, Is.Empty, "Actions without [RequirePermission] (and not in ExcludedEndpoints):\n" + string.Join("\n", missing));
    }

    private static bool HasDocAiScheme(Type controller) =>
        controller.Namespace == "ISTDocAi.UI.Server.Controllers.DocAi" &&
        controller.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Any(a => a.AuthenticationSchemes == DocAiApiKeyHandler.SchemeName);

    private static bool HasPermission(Type controller, MethodInfo action) =>
        action.IsDefined(typeof(RequirePermissionAttribute), inherit: true)
        || controller.IsDefined(typeof(RequirePermissionAttribute), inherit: true);

    private static bool IsExcluded(Type controller, MethodInfo action) =>
        ExcludedEndpoints.Contains((controller, action.Name));
}
