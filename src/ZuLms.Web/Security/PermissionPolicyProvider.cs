using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ZuLms.Web.Security;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);
        string key = policyName[RequirePermissionAttribute.Prefix.Length..];
        if (string.IsNullOrWhiteSpace(key)) return null;
        string[] keys = key.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keys.Length == 0) return null;
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(keys))
            .Build();
    }
}
