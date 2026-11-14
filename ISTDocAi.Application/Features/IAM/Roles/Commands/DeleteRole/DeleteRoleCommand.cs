using ISTDocAi.Application.Common.Contracts.IAM;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Roles.Commands.DeleteRole;

public record DeleteRoleCommand(List<RoleDeleteItem> Items) : ICommand<DeleteRoleCommandResponse>;
