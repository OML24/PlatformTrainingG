namespace ZuLms.Domain.Rules;

public static class LoginNameRules
{
    public static bool IsExternalLogin(string s) => s.Length == 4 && s.All(c => c is >= '0' and <= '9');

    public static string NormalizeLogin(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new string(s.Trim().Select(c => c switch
        {
            >= '\u0660' and <= '\u0669' => (char)('0' + c - '\u0660'),
            >= '\u06F0' and <= '\u06F9' => (char)('0' + c - '\u06F0'),
            _ => c
        }).ToArray());
    }
}
