using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Users.Commands.ChangeUserPassword;

public record ChangeUserPasswordCommand(
    string Id,
    string NewPassword,
    string? ConcurrencyStamp
) : ICommand<ChangeUserPasswordCommandResponse>;
