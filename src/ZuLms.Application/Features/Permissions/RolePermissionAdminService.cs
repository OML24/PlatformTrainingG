using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Permissions;

public sealed class RolePermissionAdminService(IAppDbContext db, IPermissionService permissions,
    IAuditLogger audit) : IRolePermissionAdminService
{
    public async Task<Result<RolePermissionMatrixDto>> GetAsync(long actorUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.roles", ct))
            return Result<RolePermissionMatrixDto>.Failure("FORBIDDEN");
        var roles = await db.Roles.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new RoleMatrixRoleDto(x.Code, x.NameAr, x.NameEn,
                db.RolePermissions.Where(rp => rp.RoleId == x.Id)
                    .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.PermKey)
                    .OrderBy(k => k).ToList())).ToListAsync(ct);
        var catalog = await db.Permissions.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.GroupKey).ThenBy(x => x.SortOrder)
            .Select(x => new RoleMatrixPermissionDto(x.PermKey, x.GroupKey, x.NameAr,
                x.NameEn, x.Scope)).ToListAsync(ct);
        return Result<RolePermissionMatrixDto>.Success(new(roles, catalog));
    }

    public async Task<Result> UpdateAsync(long actorUserId, RoleCode roleCode,
        IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.roles", ct))
            return Result.Failure("FORBIDDEN");
        var normalized = keys.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).ToHashSet(StringComparer.Ordinal);
        if (roleCode == RoleCode.Admin && !normalized.Contains("page.admin.roles"))
            return Result.Failure("ROLE_PERMISSION_PROTECTED");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Code == roleCode, ct);
        if (role is null) return Result.Failure("ROLE_NOT_FOUND");
        var catalog = await db.Permissions.Where(x => x.IsActive).ToListAsync(ct);
        if (normalized.Except(catalog.Select(x => x.PermKey), StringComparer.Ordinal).Any())
            return Result.Failure("PERMISSION_NOT_FOUND");
        var before = await db.RolePermissions.Where(x => x.RoleId == role.Id)
            .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.PermKey)
            .OrderBy(x => x).ToListAsync(ct);
        return await db.InTransactionAsync(async () =>
        {
            var existing = await db.RolePermissions.Where(x => x.RoleId == role.Id).ToListAsync(ct);
            foreach (var item in existing.Where(x => !normalized.Contains(
                catalog.Single(p => p.Id == x.PermissionId).PermKey))) db.RolePermissions.Remove(item);
            var existingIds = existing.Select(x => x.PermissionId).ToHashSet();
            foreach (var permission in catalog.Where(x => normalized.Contains(x.PermKey) &&
                !existingIds.Contains(x.Id)))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            await audit.LogAsync(new(AuditCategory.Permission, "permission.role_changed",
                $"تم تعديل صلاحيات الدور {role.NameAr}", "Role permissions changed",
                "Role", role.Id, role.Code.ToString(), BeforeJson: JsonSerializer.Serialize(before),
                AfterJson: JsonSerializer.Serialize(normalized.OrderBy(x => x)),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}
