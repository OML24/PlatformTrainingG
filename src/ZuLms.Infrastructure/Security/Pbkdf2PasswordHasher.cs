using System.Security.Cryptography;
using System.Text;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 600000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC)),
            salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedHash);
        string[] parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out int iterations)
            || iterations < 1 || iterations > 10000000)
            return false;
        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] stored = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || stored.Length != HashSize) return false;
            byte[] candidate = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC)),
                salt, iterations, HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(candidate, stored);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string storedHash)
    {
        string[] parts = storedHash.Split('$');
        return parts.Length == 4 && parts[0] == "v1" &&
            int.TryParse(parts[1], out int iterations) && iterations < Iterations;
    }
}
