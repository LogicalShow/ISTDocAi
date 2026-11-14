using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.RolePermissions.Commands.AssignRolePermissions;

public record AssignRolePermissionsCommand(string RoleId, List<string> PageActionIds) : ICommand;
