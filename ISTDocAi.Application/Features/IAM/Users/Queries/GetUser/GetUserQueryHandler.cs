using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Users.Queries.GetUser;

public class GetUserQueryHandler(IUserManagementService userManagementService) : IQueryHandler<GetUserQuery, GetUserResponse>
{
    public async Task<Result<GetUserResponse>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return Result<GetUserResponse>.Failure(Error.NotFound(ErrorCodes.UserNotFound, "User not found"));

        return Result<GetUserResponse>.Success(new GetUserResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            IsAdministrator = user.IsAdministrator,
            RoleId = user.RoleId,
            ConcurrencyStamp = user.ConcurrencyStamp
        });
    }
}
