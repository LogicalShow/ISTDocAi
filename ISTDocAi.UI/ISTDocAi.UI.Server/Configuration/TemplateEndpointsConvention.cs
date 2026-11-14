using ISTDocAi.UI.Server.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace ISTDocAi.UI.Server.Configuration;

// Remove only the template surface; future DocAi controllers remain discoverable.
public sealed class TemplateEndpointsConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers.ToArray())
        {
            var type = controller.ControllerType;
            if (type == typeof(HomeController)
                || IsWithin(type.Namespace, "ISTDocAi.UI.Server.Controllers.IAM")
                || IsWithin(type.Namespace, "ISTDocAi.UI.Server.Controllers.Public"))
            {
                application.Controllers.Remove(controller);
            }
        }
    }

    // Template updates may add nested IAM/Public namespaces; keep them gated too.
    private static bool IsWithin(string? ns, string root) =>
        ns != null && (ns == root || ns.StartsWith(root + ".", StringComparison.Ordinal));
}
