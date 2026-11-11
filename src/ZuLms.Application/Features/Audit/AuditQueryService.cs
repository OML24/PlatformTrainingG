using System.Text;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Audit;

public interface IAuditQueryService
{
    Task<Result<AuditPageDto>> ListAsync(long actorUserId, AuditFilter filter,
        CancellationToken ct);
    Task<Result<IReadOnlyList<AuditLiveDto>>> LiveAsync(long actorUserId, long afterId,
        CancellationToken ct);
    Task<Result<string>> ExportCsvAsync(long actorUserId, AuditFilter filter,
        CancellationToken ct);
}

public sealed record AuditFilter(string? Search, AuditCategory? Category,
    string? ActionCode, long? ActorUserId, long? CourseId, bool? Success,
    DateOnly? From, DateOnly? To, int Page);

public sealed record AuditRowDto(long Id, DateTimeOffset At, string ActorLogin,
    string ActorName, AuditCategory Category, string ActionCode, string EntityType,
    long? EntityId, string EntityLabel, long? CourseId, string SummaryAr,
    bool Success, string? FailureReason, string? BeforeJson, string? AfterJson);

public sealed record AuditPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<AuditRowDto> Items);

public sealed record AuditLiveDto(long Id, DateTimeOffset At, string ActorName,
    string SummaryAr, AuditCategory Category, string ActionCode, bool Success);

public sealed class AuditQueryService(IAppDbContext db, IPermissionService permissions,
    ISettingsService settings) : IAuditQueryService
{
    public async Task<Result<AuditPageDto>> ListAsync(long actorUserId,
        AuditFilter filter, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.audit", ct))
            return Result<AuditPageDto>.Failure("FORBIDDEN");
        int pageSize = await settings.GetAsync<int>("audit.page_size", ct);
        if (pageSize < 1) pageSize = 50;
        int page = filter.Page < 1 ? 1 : filter.Page;
        var query = BuildQuery(filter);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditRowDto(x.Id, x.OccurredAt,
                x.ActorLogin ?? "system",
                x.ActorName ?? "النظام",
                x.Category, x.ActionCode, x.EntityType ?? string.Empty,
                x.EntityId, x.EntityLabel ?? string.Empty,
                x.CourseId, x.SummaryAr, x.Success, x.FailureReason,
                x.BeforeJson, x.AfterJson))
            .ToListAsync(ct);
        return Result<AuditPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<IReadOnlyList<AuditLiveDto>>> LiveAsync(long actorUserId,
        long afterId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.live_feed", ct))
            return Result<IReadOnlyList<AuditLiveDto>>.Failure("FORBIDDEN");
        var rows = await db.AuditLogs.AsNoTracking()
            .Where(x => x.Id > afterId)
            .OrderBy(x => x.Id).Take(100)
            .Select(x => new AuditLiveDto(x.Id, x.OccurredAt,
                x.ActorName ?? "النظام",
                x.SummaryAr, x.Category, x.ActionCode, x.Success))
            .ToListAsync(ct);
        return Result<IReadOnlyList<AuditLiveDto>>.Success(rows);
    }

    public async Task<Result<string>> ExportCsvAsync(long actorUserId,
        AuditFilter filter, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "audit.export", ct))
            return Result<string>.Failure("FORBIDDEN");
        var rows = await BuildQuery(filter).OrderByDescending(x => x.Id).Take(100000)
            .Select(x => new
            {
                x.Id, x.OccurredAt, Actor = x.ActorLogin ?? "system",
                x.Category, x.ActionCode, x.EntityType, x.EntityId, x.EntityLabel,
                x.CourseId, x.SummaryAr, x.Success, x.FailureReason
            }).ToListAsync(ct);
        var builder = new StringBuilder();
        builder.AppendLine("id,at,actor,category,action,entity,entity_label,course,summary,success,failure");
        foreach (var row in rows)
            builder.AppendLine(string.Join(',',
                row.Id, row.OccurredAt.ToString("O"), Escape(row.Actor), row.Category,
                Escape(row.ActionCode), Escape(row.EntityType ?? string.Empty),
                Escape(row.EntityLabel ?? string.Empty),
                row.CourseId?.ToString() ?? string.Empty,
                Escape(row.SummaryAr), row.Success ? "true" : "false",
                Escape(row.FailureReason ?? string.Empty)));
        return Result<string>.Success(builder.ToString());
    }

    private IQueryable<Domain.Entities.Audit.AuditLog> BuildQuery(AuditFilter filter)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string raw = filter.Search.Trim();
            query = query.Where(x => x.SummaryAr.Contains(raw) ||
                x.ActionCode.Contains(raw) || x.EntityLabel != null &&
                x.EntityLabel.Contains(raw) ||
                x.ActorName != null && x.ActorName.Contains(raw) ||
                x.ActorLogin != null && x.ActorLogin.Contains(raw));
        }
        if (filter.Category is { } category) query = query.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(filter.ActionCode))
            query = query.Where(x => x.ActionCode == filter.ActionCode.Trim());
        if (filter.ActorUserId is long actor) query = query.Where(x => x.ActorUserId == actor);
        if (filter.CourseId is long course) query = query.Where(x => x.CourseId == course);
        if (filter.Success is { } success) query = query.Where(x => x.Success == success);
        if (filter.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.OccurredAt >= start);
        }
        if (filter.To is { } to)
        {
            var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(x => x.OccurredAt <= end);
        }
        return query;
    }

    private static string Escape(string value)
    {
        string trimmed = value.TrimStart(' ', '\t', '\r', '\n');
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
