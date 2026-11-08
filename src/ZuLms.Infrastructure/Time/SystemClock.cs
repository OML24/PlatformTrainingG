using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public TimeZoneInfo Amman { get; } = TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman");
    public DateOnly TodayAmman => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, Amman).DateTime);
}
