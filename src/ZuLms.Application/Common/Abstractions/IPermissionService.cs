using ZuLms.Domain.Enums;

namespace ZuLms.Application.Common.Abstractions;

public interface IPermissionService
{
    Task<bool> HasAsync(long userId, string key, CancellationToken cancellationToken);
    Task<AccountPermissionSet> GetAccountAsync(long userId, CancellationToken cancellationToken);
    Task<EffectivePermissionSet> GetEffectiveAsync(long userId, CancellationToken cancellationToken);
    void Invalidate(long userId);
}

public sealed record AccountPermissionSet(long UserId, AccountType AccountType, IReadOnlySet<RoleCode> Roles,
    IReadOnlySet<long> DeanScopeCenterIds, bool IsActive);

public sealed record EffectivePermissionSet(
    long UserId, RoleCode? ActiveMode, IReadOnlySet<RoleCode> Roles,
    IReadOnlySet<string> Keys, IReadOnlySet<long> DeanScopeCenterIds)
{
    public bool HasMode(RoleCode mode) => ActiveMode == mode && Roles.Contains(mode);
}
