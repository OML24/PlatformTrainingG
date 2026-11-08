using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Rules;

public static class PasswordRotationRules
{
    public static bool IsDue(AccountType accountType, DateTimeOffset? firstLoginCompletedAt,
        DateTimeOffset? passwordChangedAt, int? rotationDays, DateTimeOffset now)
    {
        if (accountType != AccountType.Employee || firstLoginCompletedAt is null || rotationDays is null)
            return false;
        DateTimeOffset changed = passwordChangedAt ?? firstLoginCompletedAt.Value;
        return now >= changed.AddDays(rotationDays.Value);
    }
}
