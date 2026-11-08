namespace ZuLms.Application.Common.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
    bool NeedsRehash(string storedHash);
}
