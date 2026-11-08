using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;
using ZuLms.Application.Features.Users.Dtos;

namespace ZuLms.Application.Features.Users;

public interface IUserService
{
    Task<Result<CredentialsDirectoryPageDto>> SearchCredentialsAsync(long actorUserId,
        string? search, CredentialsDirectoryFilter filter, int page,
        CancellationToken cancellationToken);
    Task<Result<CredentialsDirectoryPageDto>> SearchCredentialsAsync(long actorUserId,
        string? search, CredentialsDirectoryFilter filter, AccountType? accountType,
        RoleCode? role, long? centerId, bool? isActive, int page,
        CancellationToken cancellationToken);
    Task<Result<long>> CreateLocalAsync(long actorUserId, string loginName, string fullNameAr,
        string? fullNameEn, string password, UiLanguage language, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<FacultyOptionDto>>> ListFacultiesAsync(long actorUserId,
        CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<UserPermissionDto>>> ListPermissionsAsync(long actorUserId,
        long targetUserId, CancellationToken cancellationToken);
    Task<Result<UserDetailsDto>> GetDetailsAsync(long actorUserId, long targetUserId,
        CancellationToken cancellationToken);
    Task<Result> DeactivateAsync(long actorUserId, long targetUserId, uint expectedVersion,
        string reason, CancellationToken cancellationToken);
    Task<Result> RestoreAsync(long actorUserId, long targetUserId, uint expectedVersion,
        CancellationToken cancellationToken);
    Task<Result> AssignRoleAsync(long actorUserId, long targetUserId, RoleCode role,
        long? scopeCenterId, bool enabled, uint expectedVersion, CancellationToken cancellationToken);
    Task<Result> SetPermissionOverrideAsync(long actorUserId, long targetUserId,
        string permissionKey, OverrideMode? mode, uint expectedVersion, CancellationToken cancellationToken);
    Task<Result> EditAsync(long actorUserId, long targetUserId, uint expectedVersion,
        string? fullNameEn, string? emailOverride, UiLanguage language, bool accessOverrideAllow,
        string? accessOverrideReason, CancellationToken cancellationToken);
}

public enum CredentialsDirectoryFilter
{
    All, NotSent, Sent, Failed, Uncertain, NoEmail, FirstLoginPending, Disabled
}

public sealed record CredentialsDirectoryRowDto(long Id, uint Version, string FullNameAr,
    string LoginName, AccountType AccountType, string? FacultyNameAr, bool IsActive,
    bool FirstLoginCompleted, string? EffectiveEmail, CredentialsEmailStatus EmailStatus,
    DateTimeOffset? LastLoginAt, bool InitialEmailPending, int? EmpStatus,
    string? EmpStatusDesc);

public sealed record CredentialsDirectoryPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<CredentialsDirectoryRowDto> Items);

public sealed record FacultyOptionDto(long Id, string NameAr, string? NameEn);

public sealed record UserPermissionDto(string Key, string Group, string NameAr,
    string? NameEn, OverrideMode? Mode, bool Effective, bool InheritedEffective,
    IReadOnlyList<RolePermissionSourceDto> RoleSources);

public sealed record RolePermissionSourceDto(RoleCode Role, long? ScopeCenterId,
    string? ScopeCenterNameAr);
