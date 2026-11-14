using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Roles.Queries.GetRole;

public record GetRoleQuery(string Id) : IQuery<GetRoleResponse>;
