namespace ZuLms.Application.Features.Auth;

public static class AuthSessionRules
{
    public static (DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt) Expiries(
        DateTimeOffset now, int accessMinutes, bool rememberMe, int sessionHours,
        int rememberDays, DateTimeOffset? familyExpiresAt)
    {
        DateTimeOffset refresh = familyExpiresAt ?? (rememberMe
            ? now.AddDays(rememberDays) : now.AddHours(sessionHours));
        DateTimeOffset access = now.AddMinutes(accessMinutes);
        return (access <= refresh ? access : refresh, refresh);
    }
}
