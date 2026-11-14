using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.Login;

public record LoginCommand(
    string Username,
    string Password,
    string? ConnectionId = null) : ICommand<LoginCommandResponse>;
