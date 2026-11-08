using Microsoft.AspNetCore.Http;
using ZuLms.Application.Features.Auth;
using ZuLms.Web.Security;

namespace ZuLms.Web.Tests;

public sealed class AuthHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SessionCookiesAreHttpOnlySecureAndStrict(bool remember)
    {
        var context = new DefaultHttpContext();
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var session = new AuthSession("access", "refresh", now.AddMinutes(15),
            now.AddDays(30), remember, false, "/member/dashboard");
        AuthHttp.WriteCookies(context.Response, session);
        string[] cookies = context.Response.Headers.SetCookie.Select(x => x!).ToArray();
        Assert.Equal(2, cookies.Length);
        Assert.All(cookies, value =>
        {
            Assert.Contains("httponly", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/", value, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(remember, value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        });
        Assert.Contains(cookies, x => x.StartsWith(TabSession.CookieName(context, "zu_at") + "=", StringComparison.Ordinal));
        Assert.Contains(cookies, x => x.StartsWith(TabSession.CookieName(context, "zu_rt") + "=", StringComparison.Ordinal));
    }

    [Fact]
    public void LimitedSessionClearsRefreshCookie()
    {
        var context = new DefaultHttpContext();
        var expiry = new DateTimeOffset(2026, 10, 28, 12, 15, 0, TimeSpan.Zero);
        AuthHttp.WriteCookies(context.Response,
            new AuthSession("limited-access", null, expiry, null, false, true, "/change-password"));
        string[] cookies = context.Response.Headers.SetCookie.Select(x => x!).ToArray();
        Assert.Contains(cookies, x => x.StartsWith(TabSession.CookieName(context, "zu_at") + "=limited-access", StringComparison.Ordinal));
        Assert.Contains(cookies, x => x.StartsWith(TabSession.CookieName(context, "zu_rt") + "=", StringComparison.Ordinal) &&
            (x.Contains("expires=", StringComparison.OrdinalIgnoreCase) ||
             x.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(cookies, x => x.StartsWith(TabSession.CookieName(context, "zu_rt") + "=limited-access", StringComparison.Ordinal));
    }
}
