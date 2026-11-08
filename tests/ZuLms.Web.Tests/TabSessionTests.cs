using Microsoft.AspNetCore.Http;
using ZuLms.Application.Features.Auth;
using ZuLms.Web.Security;

namespace ZuLms.Web.Tests;

public sealed class TabSessionTests
{
    private const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Theory]
    [InlineData(A)]
    [InlineData(B)]
    public void SharedCookieJarNeverChoosesAnotherTabsIdentity(string id)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[TabSession.Header] = id;
        http.Request.Headers.Cookie = $"zu_at=legacy; zu_at_{A}=access-A; zu_at_{B}=access-B; zu_rt_{A}=refresh-A; zu_rt_{B}=refresh-B";
        Assert.True(TabSession.Select(http));
        Assert.Equal(id == A ? "access-A" : "access-B", TabSession.Cookie(http, AuthHttp.AccessCookie));
        Assert.Equal(id == A ? "refresh-A" : "refresh-B", TabSession.Cookie(http, AuthHttp.RefreshCookie));
    }

    [Fact]
    public void MissingSessionNeverFallsBackToGlobalOrLastLoginCookie()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"zu_at=legacy; zu_at_{A}=access-A; zu_at_{B}=access-B";
        Assert.True(TabSession.Select(http));
        Assert.Null(TabSession.Cookie(http, AuthHttp.AccessCookie));
        http.Request.QueryString = new QueryString("?zu_session=" + Guid.NewGuid().ToString("N"));
        Assert.True(TabSession.Select(http));
        Assert.Null(TabSession.Cookie(http, AuthHttp.AccessCookie));
    }

    [Theory]
    [InlineData("invalid", A)]
    [InlineData(A, "invalid")]
    [InlineData(A, B)]
    public void InvalidOrConflictingSelectorsAreRejected(string header, string query)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[TabSession.Header] = header;
        http.Request.QueryString = new QueryString("?zu_session=" + query);
        Assert.False(TabSession.Select(http));
    }

    [Fact]
    public void NavigationRefreshAndServerRedirectPreserveSelectedTab()
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?zu_session=" + A);
        Assert.True(TabSession.Select(http));
        Assert.Equal(A, TabSession.Id(http));
        string url = TabSession.Url(http, "/courses/7?view=Member&zu_session=" + B + "#lectures");
        Assert.Contains("view=Member", url);
        Assert.Contains("zu_session=" + A, url);
        Assert.DoesNotContain(B, url);
        Assert.EndsWith("#lectures", url);
        Assert.Equal(url, TabSession.Url(http, url));
        Assert.Equal("https://external.example/", TabSession.Url(http, "https://external.example/"));
    }

    [Fact]
    public async Task PrivateDocumentWithoutSelectionBootstrapsAndIsNeverCached()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Get;
        http.Request.Path = "/admin/dashboard";
        http.Request.Headers.Accept = "text/html";
        var body = new MemoryStream();
        http.Response.Body = body;
        bool called = false;
        var middleware = new TabSessionMiddleware(_ => { called = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(http);
        Assert.False(called);
        Assert.Equal("no-cache, no-store", http.Response.Headers.CacheControl);
        Assert.Equal("no-cache", http.Response.Headers.Pragma);
        body.Position = 0;
        string html = await new StreamReader(body).ReadToEndAsync();
        Assert.Contains("session-bootstrap.js", html);
        Assert.DoesNotContain("admin/dashboard", html);
    }

    [Fact]
    public async Task SelectedTabWithItsOwnCookieRendersPrivatelyAndPublicAssetsPassThrough()
    {
        var privateRequest = new DefaultHttpContext();
        privateRequest.Request.Method = HttpMethods.Get;
        privateRequest.Request.Path = "/member/dashboard";
        privateRequest.Request.Headers.Accept = "text/html";
        privateRequest.Request.Headers[TabSession.Header] = A;
        privateRequest.Request.Headers.Cookie = $"zu_at_{A}=token-A; zu_at_{B}=token-B";
        bool rendered = false;
        await new TabSessionMiddleware(_ => { rendered = true; return Task.CompletedTask; })
            .InvokeAsync(privateRequest);
        Assert.True(rendered);
        Assert.Equal("no-cache, no-store", privateRequest.Response.Headers.CacheControl);

        var asset = new DefaultHttpContext();
        asset.Request.Method = HttpMethods.Get;
        asset.Request.Path = "/js/session.js";
        bool served = false;
        await new TabSessionMiddleware(_ => { served = true; return Task.CompletedTask; }).InvokeAsync(asset);
        Assert.True(served);
        Assert.False(asset.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public void LateLogoutOrRefreshResponseOnlyTouchesItsOriginalCookieNamespace()
    {
        var oldRequest = new DefaultHttpContext();
        oldRequest.Request.Headers[TabSession.Header] = A;
        Assert.True(TabSession.Select(oldRequest));
        var newerLogin = new DefaultHttpContext();
        string newId = TabSession.New(newerLogin);
        var now = DateTimeOffset.UtcNow;
        AuthHttp.WriteCookies(newerLogin.Response, new AuthSession("new-access", "new-refresh",
            now.AddMinutes(15), now.AddHours(8), false, false, "/member/dashboard"));
        AuthHttp.ClearCookies(oldRequest.Response);
        Assert.All(oldRequest.Response.Headers.SetCookie, cookie =>
        {
            Assert.Contains("_" + A + "=", cookie!);
            Assert.DoesNotContain(newId, cookie!);
        });
        AuthHttp.WriteCookies(oldRequest.Response, new AuthSession("old-refreshed", "old-refresh",
            now.AddMinutes(15), now.AddHours(8), false, false, "/member/dashboard"));
        Assert.All(oldRequest.Response.Headers.SetCookie, cookie => Assert.DoesNotContain(newId, cookie!));
    }
}
