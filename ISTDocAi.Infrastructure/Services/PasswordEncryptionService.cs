using System.Security.Cryptography;
using System.Text;
using ISTDocAi.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ISTDocAi.Infrastructure.Services;

public class PasswordEncryptionService : IPasswordEncryptionService
{
    private const string KeyConfigPath = "Encryption:EmailPasswordKey";
    private const int Aes256KeySizeInBytes = 32;
    private const int AesIvSizeInBytes = 16;

    private readonly byte[] _key;

    public PasswordEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration[KeyConfigPath]
            ?? throw new InvalidOperationException($"{KeyConfigPath} is not configured.");

        try
        {
            _key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{KeyConfigPath} must be a valid base64 string.", ex);
        }

        if (_key.Length != Aes256KeySizeInBytes)
            throw new InvalidOperationException($"{KeyConfigPath} must decode to 32 bytes (AES-256).");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            using var aes = CreateAes();
            aes.GenerateIV();

            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(aes.IV.Concat(cipherBytes).ToArray());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to encrypt password.", ex);
        }
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            var fullBytes = Convert.FromBase64String(encryptedText);

            var iv = fullBytes[..AesIvSizeInBytes];
            var cipherBytes = fullBytes[AesIvSizeInBytes..];

            using var aes = CreateAes();
            using var decryptor = aes.CreateDecryptor(aes.Key, iv);

            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decrypt password.", ex);
        }
    }

    private Aes CreateAes()
    {
        var aes = Aes.Create();
        aes.Key = _key;
        return aes;
    }
}
