namespace ISTDocAi.Application.Features.IAM.Auth.Models;

public record AuthenticationResult(
    string AuthToken,
    string FullName,
    string Email);
