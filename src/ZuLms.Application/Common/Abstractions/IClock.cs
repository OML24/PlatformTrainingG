namespace ZuLms.Application.Common.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly TodayAmman { get; }
    TimeZoneInfo Amman { get; }
}
