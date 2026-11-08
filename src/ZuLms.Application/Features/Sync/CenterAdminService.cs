using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Sync;

public sealed class CenterAdminService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit)
    : ICenterAdminService
{
    public async Task<Result<CenterPageDto>> ListAsync(long actorUserId, string? search,
        int page, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.centers", ct))
            return Result<CenterPageDto>.Failure("FORBIDDEN");
        if (page < 1) return Result<CenterPageDto>.Failure("QUERY_INVALID");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (pageSize < 1 || page > int.MaxValue / pageSize)
            return Result<CenterPageDto>.Failure("QUERY_INVALID");
        var query = db.Centers.IgnoreQueryFilters().AsNoTracking().AsQueryable();
        string term = search?.Trim() ?? string.Empty;
        if (term.Length > 0)
            query = query.Where(x => x.NameAr.Contains(term) ||
                x.ShortCode != null && x.ShortCode.ToLower().Contains(term));
        int total = await query.CountAsync(ct);
        var centers = await query.OrderBy(x => x.SideCode).ThenBy(x => x.NameAr)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var heads = centers.Where(x => x.HeadEmployeeCode.HasValue)
            .Select(x => (x.SideCode, Code: x.HeadEmployeeCode!.Value)).ToHashSet();
        int[] sideCodes = heads.Select(x => x.SideCode).Distinct().ToArray();
        int[] employeeCodes = heads.Select(x => x.Code).Distinct().ToArray();
        var headNames = await db.Employees.AsNoTracking()
            .Where(x => sideCodes.Contains(x.SideCode) && employeeCodes.Contains(x.EmployeeCode))
            .Select(x => new { x.SideCode, x.EmployeeCode, x.FullNameAr })
            .ToListAsync(ct);
        var names = headNames.Where(x => heads.Contains((x.SideCode, x.EmployeeCode)))
            .ToDictionary(x => (x.SideCode, x.EmployeeCode), x => x.FullNameAr);
        var rows = centers.Select(x => new CenterRowDto(x.Id, x.Version, x.SideCode,
            x.CenterCode, x.NameAr, x.IsFaculty, x.ShortCode, x.IsActive,
            x.SourceMissing, x.HeadEmployeeCode.HasValue &&
                names.TryGetValue((x.SideCode, x.HeadEmployeeCode.Value), out string? name)
                    ? name : null)).ToList();
        return Result<CenterPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result> UpdateAsync(long actorUserId, long centerId,
        uint expectedVersion, bool isFaculty, string? shortCode, bool isActive,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "centers.edit", ct))
            return Result.Failure("FORBIDDEN");
        var center = await db.Centers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == centerId, ct);
        if (center is null) return Result.Failure("CENTER_NOT_FOUND");
        string? code = string.IsNullOrWhiteSpace(shortCode) ? null : shortCode.Trim().ToUpperInvariant();
        if (code is not null && !Regex.IsMatch(code, "^[A-Z]{2,10}$"))
            return Result.Failure("SHORT_CODE_INVALID");
        if (center.Version != expectedVersion) return Result.Failure("CONFLICT");
        if (center.IsFaculty == isFaculty && center.ShortCode == code && center.IsActive == isActive)
            return Result.Success();
        string before = JsonSerializer.Serialize(new { center.IsFaculty, center.ShortCode, center.IsActive });
        center.IsFaculty = isFaculty;
        center.ShortCode = code;
        center.IsActive = isActive;
        center.UpdatedAt = clock.UtcNow;
        center.UpdatedByUserId = actorUserId;
        if (!isActive)
        {
            center.DeactivatedAt = clock.UtcNow;
            center.DeactivatedByUserId = actorUserId;
            center.DeactivationReason = "إدارة المراكز";
        }
        else
        {
            center.DeactivatedAt = null;
            center.DeactivatedByUserId = null;
            center.DeactivationReason = null;
        }
        await audit.LogAsync(new AuditEntry(AuditCategory.Sync, "center.edit",
            "تعديل بيانات مركز محلية", "Local center settings changed", "Center",
            center.Id, center.NameAr, BeforeJson: before,
            AfterJson: JsonSerializer.Serialize(new { center.IsFaculty, center.ShortCode, center.IsActive }),
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
