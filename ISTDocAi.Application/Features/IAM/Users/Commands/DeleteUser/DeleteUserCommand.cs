using ISTDocAi.Application.Common.Contracts.IAM;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.DeleteUser;

public record DeleteUserCommand(List<UserDeleteItem> Items) : ICommand<DeleteUserCommandResponse>;
