using System.Text.RegularExpressions;

namespace ZuLms.Domain.Rules;

public static class NameRules
{
    public static string Join(params string?[] parts) =>
        Regex.Replace(string.Join(' ', parts.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim())), @"\s+", " ");
}
