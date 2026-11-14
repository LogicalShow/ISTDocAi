using ISTDocAi.Application.Common.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Common.Interfaces.IAM;
public interface IUserPasswordService
{
    PasswordPolicy GetPasswordPolicy();

    Task<Result> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken);
}
