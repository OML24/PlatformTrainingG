namespace ZuLms.Domain.Rules;

public static class AttendanceRules
{
    public static int MinLecturesRequired(int totalLectures, int thresholdPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalLectures);
        ArgumentOutOfRangeException.ThrowIfLessThan(thresholdPercent, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(thresholdPercent, 100);
        return totalLectures == 0 ? 0 : checked((totalLectures * thresholdPercent + 99) / 100);
    }

    public static bool IsSatisfied(int attended, int totalLectures, int thresholdPercent) =>
        totalLectures > 0 && attended >= MinLecturesRequired(totalLectures, thresholdPercent);

    public static decimal Percent(int attended, int total) =>
        total <= 0 ? 0 : decimal.Round((decimal)attended * 100 / total, 2);
}
