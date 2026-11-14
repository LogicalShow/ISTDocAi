using ISTDocAi.Infrastructure.IAM.Services;
using Microsoft.AspNetCore.Authorization;

namespace ISTDocAi.UI.Server.Authorization;

public sealed class ReportAuthorizeAttribute : AuthorizeAttribute
{
    public ReportAuthorizeAttribute() => AuthenticationSchemes = ReportAuthenticationDefaults.AuthenticationScheme;
}
