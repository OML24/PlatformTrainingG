using System.Globalization;
using System.Text;
using ZuLms.Domain.Entities.Identity;

namespace ZuLms.Domain.Rules;

public static class PasswordPolicyRules
{
    public static IReadOnlyList<string> Validate(string candidate, PasswordPolicy policy,
        IReadOnlyCollection<string> blockedNormalized, IEnumerable<string> contextWords)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);
        string normalized = candidate.Normalize(NormalizationForm.FormC);
        var errors = new List<string>();
        if (normalized.Length < policy.MinLength) errors.Add("PWD_TOO_SHORT");
        if (normalized.Length > policy.MaxLength) errors.Add("PWD_TOO_LONG");
        if (!policy.AllowSpaces && normalized.Any(char.IsWhiteSpace)) errors.Add("PWD_NO_SPACES");
        if (!policy.AllowArabic && normalized.Any(IsArabic)) errors.Add("PWD_NO_ARABIC");
        if (policy.RequireUpper && !normalized.Any(char.IsUpper)) errors.Add("PWD_NEED_UPPER");
        if (policy.RequireLower && !normalized.Any(char.IsLower)) errors.Add("PWD_NEED_LOWER");
        if (policy.RequireDigit && !normalized.Any(char.IsDigit)) errors.Add("PWD_NEED_DIGIT");
        if (policy.RequireSymbol && !normalized.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            errors.Add("PWD_NEED_SYMBOL");
        string folded = normalized.ToLowerInvariant();
        if (policy.BlockCommon && blockedNormalized.Contains(folded)) errors.Add("PWD_BLOCKED");
        // Single-character name parts (initials, "C", Arabic prefixes) would otherwise
        // reject almost every password, so only meaningful context tokens are checked.
        if (policy.BlockContext && contextWords.Any(word =>
            !string.IsNullOrWhiteSpace(word) && word.Trim().Length >= 3 &&
            folded.Contains(word.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant(), StringComparison.Ordinal)))
            errors.Add("PWD_CONTAINS_CONTEXT");
        return errors;
    }

    private static bool IsArabic(char c) =>
        CharUnicodeInfo.GetUnicodeCategory(c) is not UnicodeCategory.Control &&
        c is >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F' or >= '\u08A0' and <= '\u08FF';
}
