using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Domain.Tests;

public sealed class PasswordRotationRulesTests
{
    [Theory]
    [InlineData(AccountType.Employee, true)]
    [InlineData(AccountType.ExternalInstructor, false)]
    [InlineData(AccountType.Local, false)]
    public void OnlyEmployeeBecomesDue(AccountType accountType, bool expected)
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, PasswordRotationRules.IsDue(accountType,
            now.AddDays(-31), now.AddDays(-31), 30, now));
    }

    [Fact]
    public void UsesFirstLoginWhenNoPasswordChangeTime()
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        Assert.True(PasswordRotationRules.IsDue(AccountType.Employee,
            now.AddDays(-30), null, 30, now));
        Assert.False(PasswordRotationRules.IsDue(AccountType.Employee,
            now.AddDays(-29), null, 30, now));
    }
}
