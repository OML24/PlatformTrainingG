using Microsoft.AspNetCore.Authorization;

namespace ZuLms.Web.Security;

public sealed record PermissionRequirement(IReadOnlyList<string> Keys) : IAuthorizationRequirement
{
    public PermissionRequirement(string key) : this([key]) { }
    public string Key => Keys[0];
}
