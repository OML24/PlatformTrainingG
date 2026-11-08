namespace ZuLms.Domain.Rules;

public static class CertificateReference
{
    public static string Format(string pattern, int year, string code, int seq, int padding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seq);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(padding);
        return pattern.Replace("{YYYY}", year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{CODE}", code, StringComparison.Ordinal)
            .Replace("{NNNN}", seq.ToString($"D{padding}", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
