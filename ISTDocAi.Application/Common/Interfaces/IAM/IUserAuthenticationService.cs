using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Application.Features.IAM.Auth.Models;

namespace ISTDocAi.Application.Common.Interfaces.IAM;

public interface IUserAuthenticationService
{
    Task<Result<AuthenticationResult>> AuthenticateAsync(
        string username,
        string password,
        string? connectionId,
        CancellationToken cancellationToken);
}
