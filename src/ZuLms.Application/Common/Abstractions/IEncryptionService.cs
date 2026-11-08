namespace ZuLms.Application.Common.Abstractions;

public interface IEncryptionService
{
    string Encrypt(string plain);
    string Decrypt(string cipher);
}
