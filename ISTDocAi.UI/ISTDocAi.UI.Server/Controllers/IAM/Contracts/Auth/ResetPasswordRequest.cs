namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Auth;

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
