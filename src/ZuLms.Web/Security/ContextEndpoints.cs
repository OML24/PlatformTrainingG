using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Security;

public static class ContextEndpoints
{
    private const string CookieName = "zu_ctx";
    private static readonly (RoleCode Role, string Landing)[] Destinations =
    [
        (RoleCode.Admin, "/admin/dashboard"),
        (RoleCode.Quality, "/quality/dashboard"),
        (RoleCode.Dean, "/dean/dashboard"),
        (RoleCode.Instructor, "/instructor/courses"),
        (RoleCode.Member, "/member/dashboard"),
        (RoleCode.ExternalInstructor, "/instructor/courses")
    ];

    public static IEndpointRouteBuilder MapContextEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/context", async (HttpContext http, IPermissionService permissions) =>
        {
            if (!TryUserId(http, out long userId)) return Results.Unauthorized();
            var account = await permissions.GetAccountAsync(userId, http.RequestAborted);
            var choices = Destinations.Where(x => account.Roles.Contains(x.Role))
                .Select(x => new { role = x.Role.ToString(), landing = x.Landing }).ToArray();
            string? raw = TabSession.Cookie(http, CookieName);
            RoleCode? selected = Enum.TryParse<RoleCode>(raw, false, out var parsed) && account.Roles.Contains(parsed) ? parsed : null;
            string? current = account.Roles.Count == 0 ? null : ActiveModeResolver.Resolve(account.Roles, selected).ToString();
            return Results.Ok(new { current, choices });
        }).RequireAuthorization();

        routes.MapPost("/api/v1/context", async (HttpContext http, ContextRequest request,
            IPermissionService permissions, IAntiforgery antiforgery) =>
        {
            if (!TryUserId(http, out long userId)) return Results.Unauthorized();
            try { await antiforgery.ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(new { code = "CSRF_INVALID" }); }
            if (!Enum.TryParse<RoleCode>(request.Role, false, out var role))
                return Results.BadRequest(new { code = "CONTEXT_INVALID" });
            var destination = Destinations.FirstOrDefault(x => x.Role == role);
            if (destination.Landing is null) return Results.BadRequest(new { code = "CONTEXT_INVALID" });
            var account = await permissions.GetAccountAsync(userId, http.RequestAborted);
            if (!account.Roles.Contains(role)) return Results.Forbid();
            http.Response.Cookies.Append(TabSession.CookieName(http, CookieName), role.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });
            return Results.Ok(new { redirectUrl = TabSession.Url(http, destination.Landing) });
        }).RequireAuthorization();
        return routes;
    }

    private static bool TryUserId(HttpContext http, out long userId) =>
        long.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record ContextRequest(string Role);
}

public sealed class HttpActiveModeAccessor(IHttpContextAccessor accessor) : IActiveModeAccessor
{
    public RoleCode? SelectedMode
    {
        get
        {
            var http = accessor.HttpContext;
            var raw = http is null ? null : TabSession.Cookie(http, "zu_ctx");
            return Enum.TryParse<RoleCode>(raw, false, out var mode) ? mode : null;
        }
    }
}
