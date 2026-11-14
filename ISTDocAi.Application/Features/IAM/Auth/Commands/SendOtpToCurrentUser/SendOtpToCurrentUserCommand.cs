using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Auth.Commands.SendOtpToCurrentUser;

public record SendOtpToCurrentUserCommand : ICommand<SendOtpToCurrentUserCommandResponse>;
