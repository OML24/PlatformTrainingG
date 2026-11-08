using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Auth;
using ZuLms.Domain.Enums;
using ZuLms.Web.Localization;

namespace ZuLms.Web.Security;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/auth/csrf", (HttpContext http, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { requestToken = tokens.RequestToken });
        });

        routes.MapPost("/api/v1/auth/login", async (HttpContext http, LoginRequest request,
            IAuthService auth, ISettingsService settings, LoginRateGate rate, IAntiforgery antiforgery) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return Results.BadRequest(new { code = "CSRF_INVALID" });
            int limit = await settings.GetAsync<int>("auth.login_max_per_ip_minute", http.RequestAborted);
            if (!await rate.AllowAsync(http.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                limit, "login", http.RequestAborted))
                return Results.Json(new { code = "RATE_LIMITED" }, statusCode: StatusCodes.Status429TooManyRequests);
            var result = await auth.LoginAsync(request, http.RequestAborted);
            if (!result.Ok || result.Value is null)
                return Results.BadRequest(new { code = result.Error?.Code ?? "AUTH_INVALID" });
            // Every successful login gets its own cookie namespace, even when
            // this login page was duplicated from an already authenticated tab.
            string sessionId = TabSession.New(http);
            AuthHttp.WriteCookies(http.Response, result.Value);
            return Results.Ok(new { sessionId, redirectUrl = TabSession.Url(http, result.Value.RedirectUrl),
                passwordChangeOnly = result.Value.PasswordChangeOnly });
        });

        routes.MapPost("/api/v1/auth/refresh", async (HttpContext http, IAuthService auth,
            IAntiforgery antiforgery) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return Results.BadRequest(new { code = "CSRF_INVALID" });
            string? token = TabSession.Cookie(http, AuthHttp.RefreshCookie);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await auth.RefreshAsync(token, http.RequestAborted);
            if (!result.Ok || result.Value is null)
            {
                AuthHttp.ClearCookies(http.Response);
                return Results.Unauthorized();
            }
            AuthHttp.WriteCookies(http.Response, result.Value);
            return Results.Ok(new { redirectUrl = TabSession.Url(http, result.Value.RedirectUrl) });
        });

        routes.MapPost("/api/v1/auth/change-password", async (HttpContext http,
            ChangePasswordRequest request, IAuthService auth, IAntiforgery antiforgery) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return Results.BadRequest(new { code = "CSRF_INVALID" });
            if (!long.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId))
                return Results.Unauthorized();
            if (http.User.FindFirstValue("pwd_change") != "true") return Results.Forbid();
            var result = await auth.ChangePasswordAsync(userId, request.NewPassword,
                request.Confirmation, http.RequestAborted);
            if (!result.Ok || result.Value is null)
                return Results.BadRequest(new { code = result.Error?.Code ?? "AUTH_INVALID" });
            AuthHttp.WriteCookies(http.Response, result.Value);
            return Results.Ok(new { redirectUrl = TabSession.Url(http, result.Value.RedirectUrl) });
        }).RequireAuthorization();

        routes.MapPost("/api/v1/auth/account-password", async (HttpContext http,
            AccountPasswordRequest request, IAuthService auth, IAntiforgery antiforgery) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return Results.BadRequest(new { code = "CSRF_INVALID" });
            if (!long.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId))
                return Results.Unauthorized();
            if (http.User.FindFirstValue("pwd_change") == "true") return Results.Forbid();
            var result = await auth.ChangeOwnPasswordAsync(userId, request.CurrentPassword,
                request.NewPassword, request.Confirmation, http.RequestAborted);
            if (!result.Ok || result.Value is null)
                return Results.BadRequest(new { code = result.Error?.Code ?? "AUTH_INVALID" });
            AuthHttp.WriteCookies(http.Response, result.Value);
            return Results.Ok(new { redirectUrl = TabSession.Url(http, result.Value.RedirectUrl) });
        }).RequireAuthorization();

        routes.MapPost("/api/v1/auth/logout", async (HttpContext http, IAuthService auth,
            IAntiforgery antiforgery) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return Results.BadRequest(new { code = "CSRF_INVALID" });
            await auth.LogoutAsync(TabSession.Cookie(http, AuthHttp.RefreshCookie), http.RequestAborted);
            AuthHttp.ClearCookies(http.Response);
            return Results.Ok();
        });

        routes.MapPost("/api/v1/auth/forgot-password", async (HttpContext http,
            ForgotPasswordRequest request, IAuthService auth, IAntiforgery antiforgery) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return Results.BadRequest(new { code = "CSRF_INVALID" });
            await auth.ForgotPasswordAsync(request.LoginName, request.Email,
                http.Connection.RemoteIpAddress?.ToString() ?? string.Empty, http.RequestAborted);
            return Results.Ok(new { code = "AUTH_FORGOT_GENERIC" });
        });

        routes.MapGet("/api/v1/auth/me", async (HttpContext http, IPermissionService permissions) =>
        {
            if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            long.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
            var effective = await permissions.GetEffectiveAsync(userId, http.RequestAborted);
            return Results.Ok(new
            {
                userId,
                loginName = http.User.FindFirstValue("login"),
                fullNameAr = http.User.FindFirstValue("name"),
                preferredLanguage = (http.User.FindFirstValue("lang") ?? "ar").ToLowerInvariant(),
                roles = effective.Roles.Select(x => x.ToString()).ToArray(),
                passwordChangeOnly = http.User.FindFirstValue("pwd_change") == "true"
            });
        }).RequireAuthorization();

        routes.MapPatch("/api/v1/auth/language", async (HttpContext http, LanguageRequest request,
            IAuthService auth, IAntiforgery antiforgery) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return Results.BadRequest(new { code = "CSRF_INVALID" });
            if (!long.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId))
                return Results.Unauthorized();
            if (!Enum.TryParse<UiLanguage>(request.Language, true, out var language))
                return Results.BadRequest(new { code = "LANGUAGE_INVALID" });
            var result = await auth.SetLanguageAsync(userId, language, http.RequestAborted);
            if (!result.Ok)
                return Results.BadRequest(new { code = result.Error?.Code ?? "LANGUAGE_INVALID" });
            string code = language == UiLanguage.En ? "en" : "ar";
            http.Response.Cookies.Append(TabSession.CookieName(http, RequestCultureFromUserProvider.CookieName), code,
                new CookieOptions
                {
                    HttpOnly = false, Secure = true, SameSite = SameSiteMode.Lax,
                    Path = "/", Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true
                });
            return Results.Ok(new { language = code });
        }).RequireAuthorization();
        return routes;
    }

    private static async Task<bool> ValidCsrf(HttpContext http, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    public sealed record ChangePasswordRequest(string NewPassword, string Confirmation);
    public sealed record AccountPasswordRequest(string CurrentPassword, string NewPassword, string Confirmation);
    public sealed record ForgotPasswordRequest(string LoginName, string Email);
    public sealed record LanguageRequest(string Language);
}
