using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Security;

public sealed class CurrentUserAccessor(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public long? UserId => long.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out long id) ? id : null;
    public string? LoginName => Principal?.FindFirstValue("login");
    public string? SessionId => Principal?.FindFirstValue("jti");
    public IReadOnlyCollection<RoleCode> Roles => Principal?.FindAll(ClaimTypes.Role)
        .Select(x => Enum.TryParse<RoleCode>(x.Value, out var role) ? (RoleCode?)role : null)
        .OfType<RoleCode>().ToArray() ?? [];
    public IReadOnlyCollection<long> DeanScopeCenterIds => Principal?.FindAll("dean_scope")
        .Select(x => long.TryParse(x.Value, out long id) ? (long?)id : null)
        .OfType<long>().ToArray() ?? [];
    public string Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    public string UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty;
    public UiLanguage Language => Principal?.FindFirstValue("lang") == "en" ? UiLanguage.En : UiLanguage.Ar;
}
