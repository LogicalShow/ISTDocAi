using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Features.IAM.UserMenu.Queries.GetUserMenu;

public class GetUserMenuQueryHandler(
    IUserService userService,
    IUserMenuService userMenuService)
    : IQueryHandler<GetUserMenuQuery, UserMenuDto>
{
    public async Task<Result<UserMenuDto>> Handle(
        GetUserMenuQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userService.UserId;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure<UserMenuDto>(
                Error.Unauthorized(
                    ErrorCodes.AuthUserIdentityMissing,
                    "User identity is missing."));
        }

        return await userMenuService.GetUserMenuAsync(userId, cancellationToken);
    }
}
