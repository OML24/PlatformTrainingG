using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using ZuLms.Web.Security;

namespace ZuLms.Web.Tests;

public sealed class PermissionPolicyProviderTests
{
    [Fact]
    public async Task BuildsAuthenticatedPermissionPolicy()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        AuthorizationPolicy? policy = await provider.GetPolicyAsync("permission:page.admin.users");
        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(policy.Requirements, r => r is PermissionRequirement permission &&
            permission.Key == "page.admin.users");
    }
}
