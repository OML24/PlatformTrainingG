using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZuLms.Domain.Rules;

public static class ArabicSearchNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach (char c in value.Trim().Normalize(NormalizationForm.FormD))
        {
            if (c == '\u0640' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(c switch
            {
                'أ' or 'إ' or 'آ' => 'ا',
                'ى' => 'ي',
                'ة' => 'ه',
                _ => char.ToLowerInvariant(c)
            });
        }
        return Regex.Replace(builder.ToString(), @"\s+", " ");
    }
}
