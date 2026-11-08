using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Auth;
using ZuLms.Infrastructure.Persistence;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Security;

public static class AuthHttp
{
    public const string AccessCookie = "zu_at";
    public const string RefreshCookie = "zu_rt";

    public static void WriteCookies(HttpResponse response, AuthSession session)
    {
        var accessOptions = new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
            Path = "/", Expires = session.RememberMe ? session.AccessExpiresAt : null
        };
        if (TabSession.Id(response.HttpContext) is null) TabSession.New(response.HttpContext);
        response.Cookies.Append(TabSession.CookieName(response.HttpContext, AccessCookie), session.AccessToken, accessOptions);
        if (session.RefreshToken is not null)
        {
            var refreshOptions = new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
                Path = "/", Expires = session.RememberMe ? session.RefreshExpiresAt : null
            };
            response.Cookies.Append(TabSession.CookieName(response.HttpContext, RefreshCookie), session.RefreshToken, refreshOptions);
        }
        else response.Cookies.Delete(TabSession.CookieName(response.HttpContext, RefreshCookie),
            new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/" });
    }

    public static void ClearCookies(HttpResponse response)
    {
        var options = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/" };
        response.Cookies.Delete(TabSession.CookieName(response.HttpContext, AccessCookie), options);
        response.Cookies.Delete(TabSession.CookieName(response.HttpContext, RefreshCookie), options);
        response.Cookies.Delete(TabSession.CookieName(response.HttpContext, "zu_ctx"), options);
    }

    public static async Task ValidateUserAsync(TokenValidatedContext context)
    {
        string? sub = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? stamp = context.Principal?.FindFirstValue("stamp");
        if (!long.TryParse(sub, out long id) || !Guid.TryParse(stamp, out Guid claimedStamp))
        {
            context.Fail("Invalid session.");
            return;
        }
        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        string key = $"user-state:{id}";
        if (!cache.TryGetValue(key, out UserState? state) || state is null)
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            state = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == id)
                .Select(x => new UserState(x.IsActive, x.SecurityStamp, x.AccountType,
                    x.AccessOverrideAllow, x.Employee == null ? null : x.Employee.EmpStatus))
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (state is null) { context.Fail("Invalid session."); return; }
            cache.Set(key, state, TimeSpan.FromSeconds(30));
        }
        if (!state.IsActive || state.Stamp != claimedStamp)
        {
            context.Fail("Session revoked.");
            return;
        }
        if (state.AccountType == AccountType.Employee && !state.AccessOverrideAllow)
        {
            var settings = context.HttpContext.RequestServices.GetRequiredService<ISettingsService>();
            string codes = await settings.GetAsync<string>("access.eligible_emp_status_codes", context.HttpContext.RequestAborted);
            bool eligible = state.EmpStatus is int status && codes.Split(',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(x => int.TryParse(x, out int code) && code == status);
            if (!eligible) context.Fail("Account is not eligible.");
        }
    }

    public static void InvalidateUser(IMemoryCache cache, long id) => cache.Remove($"user-state:{id}");

    private sealed record UserState(bool IsActive, Guid Stamp, AccountType AccountType,
        bool AccessOverrideAllow, int? EmpStatus);
}

public sealed class SilentRefreshMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuthService auth, IClock clock)
    {
        if (HttpMethods.IsGet(context.Request.Method) &&
            context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            !IsAuthPath(context.Request.Path) && TabSession.Cookie(context, AuthHttp.RefreshCookie) is { Length: > 0 } refresh
            && !HasValidAccess(TabSession.Cookie(context, AuthHttp.AccessCookie), clock))
        {
            var result = await auth.RefreshAsync(refresh, context.RequestAborted);
            if (!result.Ok || result.Value is null)
            {
                AuthHttp.ClearCookies(context.Response);
                context.Response.Redirect(TabSession.Url(context, "/login"));
                return;
            }
            AuthHttp.WriteCookies(context.Response, result.Value);
            context.Request.Headers.Cookie = ReplaceAccessCookie(context.Request.Headers.Cookie.ToString(),
                TabSession.CookieName(context, AuthHttp.AccessCookie), result.Value.AccessToken);
        }
        await next(context);
    }

    private static bool IsAuthPath(PathString path) => path.StartsWithSegments("/api/v1/auth")
        || path.StartsWithSegments("/login") || path.StartsWithSegments("/logout")
        || path.StartsWithSegments("/forgot-password") || path.StartsWithSegments("/lib")
        || path.StartsWithSegments("/css") || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/fonts");

    private static bool HasValidAccess(string? raw, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(raw);
            return token.ValidTo > clock.UtcNow.UtcDateTime.AddSeconds(5);
        }
        catch (ArgumentException) { return false; }
    }

    private static string ReplaceAccessCookie(string existing, string name, string token)
    {
        string remaining = string.Join("; ", existing.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith(name + "=", StringComparison.Ordinal)));
        return string.IsNullOrEmpty(remaining) ? $"{name}={token}" : $"{remaining}; {name}={token}";
    }
}

public sealed class ForcedPasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirstValue("pwd_change") == "true" && !Allowed(context.Request.Path))
        {
            if (context.Request.Path.StartsWithSegments("/api"))
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
            else context.Response.Redirect("/change-password");
            return;
        }
        await next(context);
    }

    private static bool Allowed(PathString path) => path.StartsWithSegments("/change-password")
        // The limited identity needs its own antiforgery token before it can submit.
        || path == "/api/v1/auth/csrf"
        // MapStaticAssets fingerprints the public logo filename after rebuilding.
        || path.StartsWithSegments("/img")
        || path.StartsWithSegments("/api/v1/auth/change-password")
        || path.StartsWithSegments("/api/v1/auth/logout")
        || path.StartsWithSegments("/logout") || path.StartsWithSegments("/lib")
        || path.StartsWithSegments("/css") || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/fonts");
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "same-origin";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none'";
        if (context.Request.IsHttps)
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
        await next(context);
    }
}
