using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Features.IAM.RolePermissions.Queries.GetRolePermissions;

public record GetRolePermissionsQuery(string RoleId) : IQuery<RolePermissionsDto>;
