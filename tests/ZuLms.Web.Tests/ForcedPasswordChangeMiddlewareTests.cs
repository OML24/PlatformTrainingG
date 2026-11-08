using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZuLms.Web.Security;

namespace ZuLms.Web.Tests;

public sealed class ForcedPasswordChangeMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/auth/csrf", "GET")]
    [InlineData("/change-password", "GET")]
    [InlineData("/api/v1/auth/change-password", "POST")]
    [InlineData("/api/v1/auth/logout", "POST")]
    [InlineData("/img/logo.png", "GET")]
    [InlineData("/img/logo.lque2yrj0y.png", "GET")]
    [InlineData("/css/auth.css", "GET")]
    [InlineData("/js/api.js", "GET")]
    public async Task LimitedSessionCanReachPasswordChangeDependencies(string path, string method)
    {
        var context = Context(path, method, limited: true);
        bool reachedEndpoint = false;
        var middleware = new ForcedPasswordChangeMiddleware(_ =>
        {
            reachedEndpoint = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(reachedEndpoint);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/auth/refresh")]
    [InlineData("/api/v1/profiles/directory")]
    [InlineData("/api/v1/files/1")]
    [InlineData("/api/v1/auth/csrf/other")]
    public async Task LimitedSessionStillCannotReachOtherApis(string path)
    {
        var context = Context(path, "GET", limited: true);
        var middleware = new ForcedPasswordChangeMiddleware(_ =>
            throw new InvalidOperationException("Restricted endpoint must not run."));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task LimitedSessionCanObtainAndValidateCsrfWithoutDisablingProtection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntiforgery();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        using var provider = services.BuildServiceProvider();
        var antiforgery = provider.GetRequiredService<IAntiforgery>();
        var get = Context("/api/v1/auth/csrf", "GET", limited: true);
        get.RequestServices = provider;
        get.Request.Scheme = "https";
        string? token = null;
        var middleware = new ForcedPasswordChangeMiddleware(context =>
        {
            token = antiforgery.GetAndStoreTokens(context).RequestToken;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(get);

        Assert.False(string.IsNullOrEmpty(token));
        var post = Context("/api/v1/auth/change-password", "POST", limited: true);
        post.RequestServices = provider;
        post.Request.Scheme = "https";
        post.Request.Headers.Cookie = get.Response.Headers.SetCookie.Single()!.Split(';', 2)[0];
        post.Request.Headers["RequestVerificationToken"] = token;
        await antiforgery.ValidateRequestAsync(post);

        // A fresh request with the same cookie must still reject a forged request token.
        var invalid = Context("/api/v1/auth/change-password", "POST", limited: true);
        invalid.RequestServices = provider;
        invalid.Request.Scheme = "https";
        invalid.Request.Headers.Cookie = post.Request.Headers.Cookie;
        invalid.Request.Headers["RequestVerificationToken"] = "invalid-token";
        await Assert.ThrowsAsync<AntiforgeryValidationException>(() =>
            antiforgery.ValidateRequestAsync(invalid));
    }

    [Fact]
    public async Task LimitedSessionStillRedirectsProtectedPages()
    {
        var context = Context("/member/dashboard", "GET", limited: true);
        var middleware = new ForcedPasswordChangeMiddleware(_ =>
            throw new InvalidOperationException("Restricted page must not run."));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/change-password", context.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonLimitedRequestsContinueToNormalAuthorization(bool authenticated)
    {
        var context = Context("/member/dashboard", "GET", limited: false);
        if (!authenticated) context.User = new ClaimsPrincipal(new ClaimsIdentity());
        bool reachedNext = false;
        var middleware = new ForcedPasswordChangeMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(reachedNext);
    }

    private static DefaultHttpContext Context(string path, string method, bool limited)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "123"),
            new Claim("pwd_change", limited ? "true" : "false")
        ], "Test"));
        return context;
    }
}
