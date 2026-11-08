using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Sync;

public interface IUniversitySyncService
{
    Task<Result<long>> StartAsync(long actorUserId, CancellationToken cancellationToken);
    Task<Result<SyncRunDto>> GetRunAsync(long actorUserId, long runId,
        CancellationToken cancellationToken);
}

public sealed record SyncRunDto(long Id, DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt, SyncStatus Status, string StatsJson,
    string? ErrorText, long? TriggeredBy);
