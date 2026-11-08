using System.Security.Claims;
using Microsoft.AspNetCore.Localization;
using ZuLms.Web.Security;

namespace ZuLms.Web.Localization;

/// <summary>
/// Resolves the UI culture from the explicit <c>zu_lang</c> cookie, then the signed-in user's
/// stored preference (<c>lang</c> claim), then the Arabic default (01 §6.3).
/// </summary>
public sealed class RequestCultureFromUserProvider : IRequestCultureProvider
{
    public const string CookieName = "zu_lang";

    public Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        string? language = TabSession.Cookie(httpContext, CookieName);
        if (string.IsNullOrWhiteSpace(language) && httpContext.User?.Identity?.IsAuthenticated == true)
            language = httpContext.User.FindFirstValue("lang");
        if (string.IsNullOrWhiteSpace(language))
            return Task.FromResult<ProviderCultureResult?>(null);

        return Task.FromResult<ProviderCultureResult?>(language.Trim().ToLowerInvariant() switch
        {
            "ar" => new ProviderCultureResult("ar", "ar"),
            "en" => new ProviderCultureResult("en", "en"),
            _ => null
        });
    }
}
