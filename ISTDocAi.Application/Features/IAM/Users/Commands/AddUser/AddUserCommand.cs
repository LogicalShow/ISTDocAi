using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.AddUser;

public record AddUserCommand(
    string UserName,
    string Email,
    string? FullName,
    string Password,
    bool IsActive,
    bool IsAdministrator,
    string? RoleId
) : ICommand<AddUserCommandResponse>;
