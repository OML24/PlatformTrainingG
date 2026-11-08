using Microsoft.AspNetCore.WebUtilities;

namespace ZuLms.Web.Security;

// This is an opaque cookie selector, NOT an authentication token. Authentication
// still requires the selected HttpOnly/Secure JWT cookie and full validation.
public static class TabSession
{
    public const string Header = "X-ZU-Session";
    public const string Query = "zu_session";
    private const string ItemKey = "zu.tab-session";

    public static bool IsValid(string? value) => value is { Length: 32 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static string? Id(HttpContext http) => http.Items[ItemKey] as string;
    public static string New(HttpContext http)
    {
        string id = Guid.NewGuid().ToString("N");
        http.Items[ItemKey] = id;
        return id;
    }

    public static bool Select(HttpContext http)
    {
        string? header = http.Request.Headers[Header].Count == 1 ? http.Request.Headers[Header][0] : null;
        string? query = http.Request.Query[Query].Count == 1 ? http.Request.Query[Query][0] : null;
        if (http.Request.Headers[Header].Count > 1 || http.Request.Query[Query].Count > 1 ||
            header is not null && !IsValid(header) || query is not null && !IsValid(query) ||
            header is not null && query is not null && header != query) return false;
        if ((header ?? query) is { } id) http.Items[ItemKey] = id;
        return true;
    }

    public static string CookieName(HttpContext http, string name) =>
        Id(http) is { } id ? name + "_" + id : name + "_unselected";

    public static string? Cookie(HttpContext http, string name) =>
        Id(http) is null ? null : http.Request.Cookies[CookieName(http, name)];

    public static string Url(HttpContext http, string path)
    {
        if (Id(http) is not { } id || !path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal)) return path;
        int hashIndex = path.IndexOf('#');
        string hash = hashIndex < 0 ? "" : path[hashIndex..];
        string target = hashIndex < 0 ? path : path[..hashIndex];
        int queryIndex = target.IndexOf('?');
        var parameters = QueryHelpers.ParseQuery(queryIndex < 0 ? "" : target[queryIndex..]);
        parameters[Query] = id;
        return (queryIndex < 0 ? target : target[..queryIndex]) + QueryString.Create(parameters) + hash;
    }
    
}

public sealed class TabSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http)
    {
        if (!TabSession.Select(http))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { code = "SESSION_SELECTOR_INVALID" });
            return;
        }
        bool document = HttpMethods.IsGet(http.Request.Method) &&
            http.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
        if (!IsPublicAsset(http.Request.Path))
        {
            http.Response.Headers.CacheControl = "no-cache, no-store";
            http.Response.Headers.Pragma = "no-cache";
            // Preserve the tab selector on server-side redirects as well as links.
            http.Response.OnStarting(() =>
            {
                if (http.Response.Headers.Location.Count == 1)
                    http.Response.Headers.Location = TabSession.Url(http, http.Response.Headers.Location.ToString());
                return Task.CompletedTask;
            });
            bool hasSelectedCookies = TabSession.Cookie(http, AuthHttp.AccessCookie) is not null ||
                TabSession.Cookie(http, AuthHttp.RefreshCookie) is not null;
            if (document && !http.Request.Path.StartsWithSegments("/api") &&
                (TabSession.Id(http) is null || !hasSelectedCookies) && !IsPublicPage(http.Request.Path))
            {
                // Recover only this tab's selector before rendering any private data.
                http.Response.ContentType = "text/html; charset=utf-8";
                await http.Response.WriteAsync("<!doctype html><html><head><meta charset=\"utf-8\"><title>ZU-LMS</title><script src=\"/js/session-bootstrap.js\" defer></script></head><body></body></html>");
                return;
            }
        }
        await next(http);
    }

    private static bool IsPublicAsset(PathString path) => path.StartsWithSegments("/js") ||
        path.StartsWithSegments("/css") || path.StartsWithSegments("/lib") ||
        path.StartsWithSegments("/fonts") || path.StartsWithSegments("/img") ||
        path.Value?.EndsWith(".styles.css", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsPublicPage(PathString path) => path == "/login" || path == "/forgot-password" ||
        path == "/verify" || path == "/access-denied" || path == "/Error" || path == "/health";
}
