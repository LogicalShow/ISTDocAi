using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace ISTDocAi.Application.Services.DocAi.Webhooks;

public static class WebhookSignature
{
    public const string TimestampHeader = "X-Timestamp";
    public const string SignatureHeader = "X-Signature";

    public static bool LooksValid(string? timestamp, string? signature) =>
        timestamp is { Length: > 0 and <= 12 } && timestamp.All(c => c is >= '0' and <= '9') &&
        signature is { Length: 71 } && signature.StartsWith("sha256=", StringComparison.Ordinal) &&
        signature.Skip(7).All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    // Secrets are decoded bytes, not the UTF-8 bytes of their Base64 representation.
    public static string Compute(ReadOnlySpan<byte> secret, string timestamp, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(timestamp);
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, secret);
        hmac.AppendData(Encoding.UTF8.GetBytes(timestamp + "."));
        hmac.AppendData(body);
        return "sha256=" + Convert.ToHexStringLower(hmac.GetHashAndReset());
    }

    public static bool Verify(string? timestamp, ReadOnlySpan<byte> body, string? signature,
        ReadOnlySpan<byte> secret1, ReadOnlySpan<byte> secret2 = default)
    {
        if (!LooksValid(timestamp, signature)) return false;
        var supplied = Convert.FromHexString(signature![7..]);
        var first = Convert.FromHexString(Compute(secret1, timestamp!, body)[7..]);
        bool valid = CryptographicOperations.FixedTimeEquals(first, supplied);
        if (!secret2.IsEmpty)
        {
            var second = Convert.FromHexString(Compute(secret2, timestamp!, body)[7..]);
            valid |= CryptographicOperations.FixedTimeEquals(second, supplied);
        }
        return valid;
    }

    public static bool IsFresh(string? timestamp, DateTimeOffset now, TimeSpan window)
    {
        if (window < TimeSpan.Zero || timestamp is not { Length: > 0 and <= 12 } ||
            !timestamp.All(c => c is >= '0' and <= '9') ||
            !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out long unix)) return false;
        // Decimal subtraction avoids overflow even for extreme caller-supplied windows.
        return Math.Abs((decimal)unix - now.ToUnixTimeSeconds()) <= window.Ticks / (decimal)TimeSpan.TicksPerSecond;
    }
}
