using ZuLms.Application.Features.Auth;

namespace ZuLms.Application.Tests;

public sealed class AuthSessionRulesTests
{
    [Fact]
    public void RotationKeepsOriginalAbsoluteExpiry()
    {
        var initial = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var first = AuthSessionRules.Expiries(initial, 15, false, 12, 30, null);
        var rotated = AuthSessionRules.Expiries(initial.AddHours(11).AddMinutes(55),
            15, false, 12, 30, first.RefreshExpiresAt);
        Assert.Equal(initial.AddHours(12), first.RefreshExpiresAt);
        Assert.Equal(first.RefreshExpiresAt, rotated.RefreshExpiresAt);
        Assert.Equal(first.RefreshExpiresAt, rotated.AccessExpiresAt);
    }

    [Fact]
    public void RememberedSessionAlsoKeepsOriginalExpiry()
    {
        var initial = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var first = AuthSessionRules.Expiries(initial, 15, true, 12, 30, null);
        var rotated = AuthSessionRules.Expiries(initial.AddDays(20), 15,
            true, 12, 30, first.RefreshExpiresAt);
        Assert.Equal(initial.AddDays(30), rotated.RefreshExpiresAt);
    }
}
