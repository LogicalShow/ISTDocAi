using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Features.IAM.UserPermissions.Queries.GetUserPermissions;

public record GetUserPermissionsQuery(string UserId) : IQuery<UserPermissionsDto>;
