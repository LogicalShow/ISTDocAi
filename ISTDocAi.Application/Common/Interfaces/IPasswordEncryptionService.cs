namespace ISTDocAi.Application.Common.Interfaces;

public interface IPasswordEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string encryptedText);
}
