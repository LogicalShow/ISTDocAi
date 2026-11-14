using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Utilities;
using System.Collections.Concurrent;

namespace ISTDocAi.Infrastructure.Services;

public class OtpService(TimeProvider timeProvider) : IOtpService
{
    private static readonly ConcurrentDictionary<string, (string OtpCode, DateTime Expiry)> _store = new();

    public void SaveOtp(string email, string otp, TimeSpan validFor)
    {
        _store[email.ToLower()] = (otp, timeProvider.GetLocalDateTime().Add(validFor));
    }

    public bool TryGetOtp(string email, out string otp)
    {
        otp = "";
        if (_store.TryGetValue(email.ToLower(), out var entry))
        {
            if (timeProvider.GetLocalDateTime() <= entry.Expiry)
            {
                otp = entry.OtpCode;
                return true;
            }
            else
            {
                _store.TryRemove(email.ToLower(), out _); // expired
            }
        }
        return false;
    }

    public void RemoveOtp(string email)
    {
        _store.TryRemove(email.ToLower(), out _);
    }
}
