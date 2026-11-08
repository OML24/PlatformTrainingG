using System.Security.Cryptography;
using ZuLms.Domain.Entities.Identity;

namespace ZuLms.Domain.Rules;

public static class InitialPasswordRules
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#%*-_+=?";
    private const string Alphabet = Upper + Lower + Digits + Symbols;

    public static string Generate(PasswordPolicy policy, IReadOnlyCollection<string> blockedNormalized,
        IEnumerable<string> contextWords)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.MinLength < 1 || policy.MaxLength < policy.MinLength)
            throw new InvalidOperationException("The current password policy has an invalid length range.");
        int length = Math.Clamp(16, policy.MinLength, policy.MaxLength);
        int requiredCount = (policy.RequireUpper ? 1 : 0) + (policy.RequireLower ? 1 : 0) +
            (policy.RequireDigit ? 1 : 0) + (policy.RequireSymbol ? 1 : 0);
        if (requiredCount > length)
            throw new InvalidOperationException("No initial password satisfies the current password policy.");
        string[] context = contextWords.ToArray();
        char[] characters = new char[length];
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int position = 0;
            if (policy.RequireUpper) characters[position++] = Pick(Upper);
            if (policy.RequireLower) characters[position++] = Pick(Lower);
            if (policy.RequireDigit) characters[position++] = Pick(Digits);
            if (policy.RequireSymbol) characters[position++] = Pick(Symbols);
            while (position < length) characters[position++] = Pick(Alphabet);
            for (int i = length - 1; i > 0; i--)
            {
                int other = RandomNumberGenerator.GetInt32(i + 1);
                (characters[i], characters[other]) = (characters[other], characters[i]);
            }
            string candidate = new(characters);
            if (PasswordPolicyRules.Validate(candidate, policy, blockedNormalized, context).Count == 0)
                return candidate;
        }
        throw new InvalidOperationException("No initial password satisfies the current password policy.");
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
