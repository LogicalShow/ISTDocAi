namespace ISTDocAi.Application.Features.IAM.Auth.Commands.Login;

public record LoginCommandResponse(
    string AuthToken,
    string FullName,
    string Email);
