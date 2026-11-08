namespace ZuLms.Application.Common.Abstractions;

using ZuLms.Application.Common.Results;

public interface ISettingsService
{
    Task<Result<IReadOnlyList<SystemSettingDto>>> ListAsync(long actorUserId,
        CancellationToken cancellationToken);
    Task<T> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task<Result> SetAsync(string key, string value, uint expectedVersion, long actorUserId, CancellationToken cancellationToken);
    void Invalidate(string key);
}

public sealed record SystemSettingDto(string Key, string Group, string Type, string? Value,
    string? DefaultValue, decimal? MinValue, decimal? MaxValue, string LabelAr,
    string? LabelEn, string? DescriptionAr, string? DescriptionEn, bool IsSecret,
    uint Version, int SortOrder);
