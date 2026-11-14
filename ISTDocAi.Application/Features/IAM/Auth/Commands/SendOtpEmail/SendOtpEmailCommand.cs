using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.SendOtpEmail;

public record SendOtpEmailCommand(
    string Email
) : ICommand<SendOtpEmailCommandResponse>;