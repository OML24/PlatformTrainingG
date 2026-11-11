using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Permissions;

public sealed class PermissionService(IAppDbContext db, IActiveModeAccessor activeMode) : IPermissionService
{
    private readonly Dictionary<(long UserId, RoleCode? Mode), EffectivePermissionSet> cache = new();

    public async Task<bool> HasAsync(long userId, string key, CancellationToken cancellationToken) =>
        (await GetEffectiveAsync(userId, cancellationToken)).Keys.Contains(key);

    public async Task<AccountPermissionSet> GetAccountAsync(long userId, CancellationToken ct)
    {
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new { x.IsActive, x.AccountType, x.EmployeeId }).SingleOrDefaultAsync(ct);
        if (user is null || !user.IsActive)
            return new(userId, user?.AccountType ?? AccountType.Local, new HashSet<RoleCode>(), new HashSet<long>(), false);
        var assignments = await db.UserRoles.AsNoTracking().Where(x => x.UserId == userId && x.IsActive)
            .Select(x => new { x.RoleId, x.ScopeCenterId }).ToListAsync(ct);
        var roleIds = assignments.Select(x => x.RoleId).ToHashSet();
        if (await db.Courses.AsNoTracking().AnyAsync(x => x.InstructorUserId == userId &&
                (x.Status == CourseStatus.Approved || x.Status == CourseStatus.Finished), ct))
            roleIds.Add(await db.Roles.Where(x => x.Code == RoleCode.Instructor).Select(x => x.Id).SingleAsync(ct));
        if (user.AccountType == AccountType.ExternalInstructor)
            roleIds.Add(await db.Roles.Where(x => x.Code == RoleCode.ExternalInstructor).Select(x => x.Id).SingleAsync(ct));
        var roleCodes = await db.Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Code }).ToListAsync(ct);
        var roles = roleCodes.Select(x => x.Code).ToList();
        long? managerRoleId = roleCodes.Where(x => x.Code == RoleCode.CenterManager).Select(x => (long?)x.Id).FirstOrDefault();
        // Center-manager scopes are kept apart so a person who is both dean and manager
        // never sees the manager's center while acting as dean (and vice versa).
        var managerScopes = assignments.Where(x => x.ScopeCenterId.HasValue && x.RoleId == managerRoleId)
            .Select(x => x.ScopeCenterId!.Value).ToHashSet();
        var scopes = assignments.Where(x => x.ScopeCenterId.HasValue && x.RoleId != managerRoleId)
            .Select(x => x.ScopeCenterId!.Value).ToHashSet();
        if (scopes.Count == 0 && user.EmployeeId.HasValue)
        {
            long? center = await db.Employees.Where(x => x.Id == user.EmployeeId).Select(x => x.CenterId).SingleOrDefaultAsync(ct);
            if (center.HasValue) scopes.Add(center.Value);
        }
        return new(userId, user.AccountType, roles.ToHashSet(), scopes, true, managerScopes);
    }

    public async Task<EffectivePermissionSet> GetEffectiveAsync(long userId, CancellationToken ct)
    {
        var account = await GetAccountAsync(userId, ct);
        var mode = ActiveModeResolver.Resolve(account.Roles, activeMode.SelectedMode);
        if (cache.TryGetValue((userId, mode), out var saved)) return saved;
        if (!account.IsActive || mode is null || !account.Roles.Contains(mode.Value)) return Empty(userId, mode);
        var roleId = await db.Roles.AsNoTracking().Where(x => x.Code == mode.Value).Select(x => x.Id).SingleAsync(ct);
        var grantedIds = await db.RolePermissions.AsNoTracking().Where(x => x.RoleId == roleId)
            .Select(x => x.PermissionId).ToListAsync(ct);
        var valid = await db.Permissions.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new { x.Id, x.PermKey }).ToDictionaryAsync(x => x.Id, x => x.PermKey, ct);
        var granted = grantedIds.Where(valid.ContainsKey).Select(id => valid[id]);
        var overrides = await db.UserPermissionOverrides.AsNoTracking().Where(x => x.UserId == userId)
            .Where(x => valid.Keys.Contains(x.PermissionId)).Select(x => new { x.PermissionId, x.Mode }).ToListAsync(ct);
        var effectiveOverrides = overrides.Select(x => (valid[x.PermissionId], x.Mode));
        var keys = PermissionSetRules.ForMode(mode.Value, granted, effectiveOverrides, account.AccountType);
        IReadOnlySet<long> scopes = mode == RoleCode.Dean ? account.DeanScopeCenterIds
            : mode == RoleCode.CenterManager ? account.ManagerScopeCenterIds ?? new HashSet<long>()
            : new HashSet<long>();
        var result = new EffectivePermissionSet(userId, mode, new HashSet<RoleCode> { mode.Value }, keys, scopes);
        cache[(userId, mode)] = result;
        return result;
    }

    public void Invalidate(long userId)
    {
        foreach (var key in cache.Keys.Where(x => x.UserId == userId).ToArray()) cache.Remove(key);
    }

    private static EffectivePermissionSet Empty(long id, RoleCode? mode) => new(id, mode,
        new HashSet<RoleCode>(), new HashSet<string>(StringComparer.Ordinal), new HashSet<long>());
}
