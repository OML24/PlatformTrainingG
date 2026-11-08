using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Notifications;

public interface INotificationService
{
    Task<Result<NotificationSummaryDto>> ListAsync(long actorUserId, bool unreadOnly,
        CancellationToken ct);
    Task<Result> MarkReadAsync(long actorUserId, long notificationId, CancellationToken ct);
    Task<Result<int>> MarkAllReadAsync(long actorUserId, CancellationToken ct);
    Task<Result<int>> MarkLinksReadAsync(long actorUserId, IReadOnlyCollection<string> links,
        CancellationToken ct);
}

public sealed record NotificationRowDto(long Id, string TitleAr, string BodyAr,
    string? LinkUrl, DateTimeOffset CreatedAt, bool IsRead);

public sealed record NotificationLinkCountDto(string LinkUrl, int Count);

public sealed record NotificationSummaryDto(int Unread,
    IReadOnlyList<NotificationRowDto> Latest)
{
    /// <summary>Unread notifications grouped by target link, used for the sidebar badges.</summary>
    public IReadOnlyList<NotificationLinkCountDto> UnreadByLink { get; init; } = [];
}

public sealed class NotificationService(IAppDbContext db) : INotificationService
{
    public async Task<Result<NotificationSummaryDto>> ListAsync(long actorUserId,
        bool unreadOnly, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking()
            .Where(x => x.UserId == actorUserId && x.IsActive);
        int unread = await query.CountAsync(x => x.ReadAt == null, ct);
        var latest = await query
            .Where(x => !unreadOnly || x.ReadAt == null)
            .OrderByDescending(x => x.Id).Take(20)
            .Select(x => new NotificationRowDto(x.Id, x.TitleAr, x.BodyAr,
                x.LinkUrl, x.CreatedAt, x.ReadAt != null))
            .ToListAsync(ct);
        var unreadByLink = await query
            .Where(x => x.ReadAt == null && x.LinkUrl != null)
            .GroupBy(x => x.LinkUrl!)
            .Select(g => new NotificationLinkCountDto(g.Key, g.Count()))
            .ToListAsync(ct);
        return Result<NotificationSummaryDto>.Success(new(unread, latest)
        {
            UnreadByLink = unreadByLink
        });
    }

    public async Task<Result> MarkReadAsync(long actorUserId, long notificationId,
        CancellationToken ct)
    {
        var notification = await db.Notifications
            .SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == actorUserId, ct);
        if (notification is null) return Result.Failure("NOTIFICATION_NOT_FOUND");
        notification.ReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<int>> MarkAllReadAsync(long actorUserId, CancellationToken ct)
    {
        var pending = await db.Notifications
            .Where(x => x.UserId == actorUserId && x.ReadAt == null && x.IsActive)
            .ToListAsync(ct);
        foreach (var notification in pending)
        {
            notification.ReadAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(pending.Count);
    }

    public async Task<Result<int>> MarkLinksReadAsync(long actorUserId,
        IReadOnlyCollection<string> links, CancellationToken ct)
    {
        var wanted = links.Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 500)
            .Distinct(StringComparer.Ordinal).Take(100).ToList();
        if (wanted.Count == 0) return Result<int>.Success(0);
        var pending = await db.Notifications
            .Where(x => x.UserId == actorUserId && x.ReadAt == null && x.IsActive &&
                x.LinkUrl != null && wanted.Contains(x.LinkUrl!))
            .ToListAsync(ct);
        foreach (var notification in pending)
        {
            notification.ReadAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(pending.Count);
    }
}
