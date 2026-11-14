using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.UserPermissions.Commands.AssignUserPermissions;

public record AssignUserPermissionsCommand(string UserId, List<string> PageActionIds,string? SourceRoleId) : ICommand;
