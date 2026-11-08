using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Security;

public sealed class AesGcmEncryptionService : IEncryptionService
{
    private static readonly byte[] Aad = "zu-lms-v1"u8.ToArray();
    private readonly string currentKid;
    private readonly IReadOnlyDictionary<string, byte[]> keys;

    public AesGcmEncryptionService(string keyDocument)
    {
        using JsonDocument json = JsonDocument.Parse(keyDocument);
        currentKid = json.RootElement.GetProperty("current").GetString()
            ?? throw new InvalidOperationException("Current encryption key ID is missing.");
        var parsed = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (JsonProperty item in json.RootElement.GetProperty("keys").EnumerateObject())
        {
            byte[] key = Convert.FromBase64String(item.Value.GetString() ?? "");
            if (key.Length != 32) throw new InvalidOperationException("Encryption keys must be 32 bytes.");
            parsed[item.Name] = key;
        }
        if (!parsed.ContainsKey(currentKid)) throw new InvalidOperationException("Current encryption key ID is unknown.");
        keys = parsed;
    }

    public string Encrypt(string plain)
    {
        ArgumentNullException.ThrowIfNull(plain);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] data = Encoding.UTF8.GetBytes(plain.Normalize(NormalizationForm.FormC));
        byte[] cipher = new byte[data.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(keys[currentKid], tag.Length);
        aes.Encrypt(nonce, data, cipher, tag, Aad);
        return $"v1:{currentKid}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(cipher)}:{Convert.ToBase64String(tag)}";
    }

    public string Decrypt(string encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        string[] parts = encrypted.Split(':');
        if (parts.Length != 5 || parts[0] != "v1" || !keys.TryGetValue(parts[1], out byte[]? key))
            throw new CryptographicException("Invalid encrypted value.");
        byte[] nonce = Convert.FromBase64String(parts[2]);
        byte[] cipher = Convert.FromBase64String(parts[3]);
        byte[] tag = Convert.FromBase64String(parts[4]);
        if (nonce.Length != 12 || tag.Length != 16) throw new CryptographicException("Invalid encrypted value.");
        byte[] plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain, Aad);
        return Encoding.UTF8.GetString(plain);
    }
}
