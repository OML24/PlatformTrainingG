using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Users.Dtos;

public sealed record UserDetailsDto(long Id, uint Version, string LoginName,
    string FullNameAr, string? FullNameEn, AccountType AccountType, bool IsActive,
    string? EffectiveEmail, string? EmailOverride, UiLanguage PreferredLanguage,
    bool AccessOverrideAllow, string? AccessOverrideReason, IReadOnlyList<UserRoleDto> Roles);

public sealed record UserRoleDto(RoleCode Role, long? ScopeCenterId);
