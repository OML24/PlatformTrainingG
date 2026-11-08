using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Permissions;

public interface IRolePermissionAdminService
{
    Task<Result<RolePermissionMatrixDto>> GetAsync(long actorUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long actorUserId, RoleCode role, IReadOnlyCollection<string> keys,
        CancellationToken ct);
}

public sealed record RolePermissionMatrixDto(IReadOnlyList<RoleMatrixRoleDto> Roles,
    IReadOnlyList<RoleMatrixPermissionDto> Permissions);
public sealed record RoleMatrixRoleDto(RoleCode Code, string NameAr, string? NameEn,
    IReadOnlyList<string> PermissionKeys);
public sealed record RoleMatrixPermissionDto(string Key, string Group, string NameAr,
    string? NameEn, PermissionScope Scope);
