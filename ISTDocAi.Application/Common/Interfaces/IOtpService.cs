namespace ISTDocAi.Application.Common.Interfaces;

public interface IOtpService
{
    void SaveOtp(string email, string otp, TimeSpan validFor);
    bool TryGetOtp(string email, out string otp);
    void RemoveOtp(string email);
}
