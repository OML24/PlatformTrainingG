using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Hints;

public interface IHintService
{
    /// <summary>Hints displayed on a page; every signed-in user may read them (01 §6.4).</summary>
    Task<Result<IReadOnlyList<HintDto>>> ListByContextAsync(string? context, CancellationToken cancellationToken);

    Task<Result<HintPageDto>> ListAllAsync(long actorUserId, string? search, int page,
        CancellationToken cancellationToken);

    Task<Result<long>> CreateAsync(long actorUserId, SaveHintInput input, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(long actorUserId, long id, SaveHintInput input, CancellationToken cancellationToken);

    Task<Result> DeactivateAsync(long actorUserId, long id, CancellationToken cancellationToken);
}

public sealed record HintDto(string FieldKey, string HintAr, string? HintEn);

public sealed record HintAdminDto(long Id, string FieldKey, string ContextKey, string HintAr,
    string? HintEn, bool IsEnabled, uint Version);

public sealed record HintPageDto(int Page, int PageSize, int Total, IReadOnlyList<HintAdminDto> Items);

public sealed record SaveHintInput(string FieldKey, string ContextKey, string HintAr,
    string? HintEn, bool IsEnabled);
