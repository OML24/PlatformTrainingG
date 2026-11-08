using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Sync;

public interface ICenterAdminService
{
    Task<Result<CenterPageDto>> ListAsync(long actorUserId, string? search, int page,
        CancellationToken ct);
    Task<Result> UpdateAsync(long actorUserId, long centerId, uint expectedVersion,
        bool isFaculty, string? shortCode, bool isActive, CancellationToken ct);
}

public sealed record CenterRowDto(long Id, uint Version, int SideCode, int CenterCode,
    string NameAr, bool IsFaculty, string? ShortCode, bool IsActive, bool SourceMissing,
    string? SuggestedHeadName);
public sealed record CenterPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<CenterRowDto> Items);
