using System.Security.Cryptography;
using System.Text.Json;
using ZuLms.Infrastructure.Security;

namespace ZuLms.Infrastructure.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Pbkdf2RoundTripAndWrongPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        string hashed = hasher.Hash("كلمة مرور تجريبية 123");
        Assert.StartsWith("v1$600000$", hashed);
        Assert.True(hasher.Verify("كلمة مرور تجريبية 123", hashed));
        Assert.False(hasher.Verify("كلمة مرور أخرى", hashed));
        Assert.False(hasher.NeedsRehash(hashed));
        Assert.True(hasher.NeedsRehash(hashed.Replace("$600000$", "$500000$", StringComparison.Ordinal)));
    }

    [Fact]
    public void AesGcmRoundTripAndTamperDetection()
    {
        string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var service = new AesGcmEncryptionService(JsonSerializer.Serialize(new
        {
            current = "test",
            keys = new Dictionary<string, string> { ["test"] = key }
        }));
        string cipher = service.Encrypt("قيمة تجريبية");
        Assert.StartsWith("v1:test:", cipher);
        Assert.Equal("قيمة تجريبية", service.Decrypt(cipher));
        string[] parts = cipher.Split(':');
        byte[] tag = Convert.FromBase64String(parts[4]);
        tag[0] ^= 1;
        parts[4] = Convert.ToBase64String(tag);
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(string.Join(':', parts)));
    }
}
