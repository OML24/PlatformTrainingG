using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Settings;

public interface IPasswordPolicyService
{
    Task<Result<PasswordPolicyDto>> GetAsync(long actorUserId, CancellationToken cancellationToken);
    Task<Result> UpdateAsync(long actorUserId, PasswordPolicyDto policy, CancellationToken cancellationToken);
    Task<Result<BlockedPasswordPageDto>> ListBlockedAsync(long actorUserId,
        string? search, int page, CancellationToken cancellationToken);
    Task<Result> AddBlockedAsync(long actorUserId, string password, CancellationToken cancellationToken);
    Task<Result> RemoveBlockedAsync(long actorUserId, long id, CancellationToken cancellationToken);
}

public sealed record PasswordPolicyDto(int MinLength, int MaxLength, bool AllowSpaces,
    bool AllowArabic, bool BlockCommon, bool BlockContext, bool RequireUpper,
    bool RequireLower, bool RequireDigit, bool RequireSymbol, int? RotationDays);

public sealed record BlockedPasswordDto(long Id, string PasswordNormalized, string Source);
public sealed record BlockedPasswordPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<BlockedPasswordDto> Items);
