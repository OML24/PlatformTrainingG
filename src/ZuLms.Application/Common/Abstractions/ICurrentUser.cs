using ZuLms.Domain.Enums;

namespace ZuLms.Application.Common.Abstractions;

public interface ICurrentUser
{
    long? UserId { get; }
    string? LoginName { get; }
    string? SessionId { get; }
    IReadOnlyCollection<RoleCode> Roles { get; }
    IReadOnlyCollection<long> DeanScopeCenterIds { get; }
    string Ip { get; }
    string UserAgent { get; }
    UiLanguage Language { get; }
}
