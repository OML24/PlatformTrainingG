using Microsoft.AspNetCore.Authorization;

namespace ZuLms.Web.Security;

/// <summary>
/// Requires at least one of the given permission keys (03 §11). Use a single key when the endpoint
/// maps to exactly one action.
/// </summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "permission:";
    private const char Separator = '|';

    public RequirePermissionAttribute(params string[] keys)
    {
        ArgumentOutOfRangeException.ThrowIfZero(keys.Length);
        Policy = Prefix + string.Join(Separator, keys);
    }
}
