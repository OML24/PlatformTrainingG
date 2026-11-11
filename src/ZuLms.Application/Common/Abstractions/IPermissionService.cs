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
    IReadOnlySet<long> DeanScopeCenterIds, bool IsActive,
    IReadOnlySet<long>? ManagerScopeCenterIds = null);

public sealed record EffectivePermissionSet(
    long UserId, RoleCode? ActiveMode, IReadOnlySet<RoleCode> Roles,
    IReadOnlySet<string> Keys, IReadOnlySet<long> DeanScopeCenterIds)
{
    public bool HasMode(RoleCode mode) => ActiveMode == mode && Roles.Contains(mode);

    /// <summary>Dean (over a faculty) or center manager (over a center/department): same powers,
    /// limited to <see cref="DeanScopeCenterIds"/> which holds the active role's own scope.</summary>
    public bool IsUnitHead => HasMode(RoleCode.Dean) || HasMode(RoleCode.CenterManager);
}
