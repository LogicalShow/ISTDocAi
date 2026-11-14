using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Common.Interfaces.IAM;

public interface IUserMenuService
{
    Task<Result<UserMenuDto>> GetUserMenuAsync(
        string userId,
        CancellationToken cancellationToken);
}
