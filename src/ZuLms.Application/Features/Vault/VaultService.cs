using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Files;
using ZuLms.Domain.Entities.Files;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Vault;

public interface IVaultService
{
    /// <summary>Links a manually uploaded official document into the central vault.</summary>
    Task<Result> LinkAsync(long actorUserId, long fileId, FileKind docType,
        string titleAr, long? courseId, long? centerId, CancellationToken ct);
    Task<Result<VaultPageDto>> ListAsync(long actorUserId, string? search,
        FileKind? docType, long? courseId, long? centerId, int page,
        CancellationToken ct);
    Task<Result<FileDownloadDto>> GetFileAsync(long actorUserId, long documentId,
        CancellationToken ct);
    Task<Result> DeactivateAsync(long actorUserId, long documentId, string reason,
        CancellationToken ct);
}

public sealed record VaultRowDto(long Id, long FileId, FileKind DocType,
    string TitleAr, long? CourseId, string? CourseNo, long? CenterId,
    string? CenterNameAr, string UploadedByNameAr, DateTimeOffset UploadedAt);

public sealed record VaultPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<VaultRowDto> Items);

public sealed class VaultService(IAppDbContext db, IPermissionService permissions,
    ISettingsService settings, IFileUploadService files, IAuditLogger audit)
    : IVaultService
{
    private static readonly FileKind[] ManualKinds =
        [FileKind.QualityMinutes, FileKind.Recommendation, FileKind.Other];

    public async Task<Result> LinkAsync(long actorUserId, long fileId, FileKind docType,
        string titleAr, long? courseId, long? centerId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "vault.upload", ct))
            return Result.Failure("FORBIDDEN");
        if (!ManualKinds.Contains(docType)) return Result.Failure("VAULT_KIND_INVALID");
        if (string.IsNullOrWhiteSpace(titleAr) || titleAr.Trim().Length > 300)
            return Result.Failure("VAULT_TITLE_REQUIRED");
        var file = await db.StoredFiles.AsNoTracking()
            .Where(x => x.Id == fileId && x.IsActive && x.UploadedBy == actorUserId)
            .Select(x => new { x.Kind }).SingleOrDefaultAsync(ct);
        if (file is null) return Result.Failure("FILE_NOT_FOUND");
        if (file.Kind != docType) return Result.Failure("VAULT_KIND_MISMATCH");
        if (await db.VaultDocuments.AsNoTracking().AnyAsync(x =>
            x.FileId == fileId && x.IsActive, ct))
            return Result.Failure("VAULT_ALREADY_LINKED");
        if (courseId is long course &&
            !await db.Courses.IgnoreQueryFilters().AnyAsync(x => x.Id == course, ct))
            return Result.Failure("COURSE_NOT_FOUND");
        if (centerId is long center &&
            !await db.Centers.AnyAsync(x => x.Id == center, ct))
            return Result.Failure("CENTER_NOT_FOUND");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var document = new VaultDocument
        {
            FileId = fileId, DocType = docType, TitleAr = titleAr.Trim(),
            RelatedCourseId = courseId, RelatedCenterId = centerId,
            SourceEntityType = "StoredFile", SourceEntityId = fileId,
            UploadedBy = actorUserId, UploadedAt = now,
            CreatedAt = now, CreatedByUserId = actorUserId, IsActive = true
        };
        db.VaultDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(new AuditEntry(AuditCategory.Document, "vault.upload",
            "ربط وثيقة في المستودع", "Vault document linked", "VaultDocument",
            document.Id, document.TitleAr, ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<VaultPageDto>> ListAsync(long actorUserId, string? search,
        FileKind? docType, long? courseId, long? centerId, int page,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.vault", ct))
            return Result<VaultPageDto>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1) page = 1;
        string raw = (search ?? string.Empty).Trim();
        var query = db.VaultDocuments.AsNoTracking();
        if (raw.Length > 0)
            query = query.Where(x => x.TitleAr.Contains(raw));
        if (docType is { } kind) query = query.Where(x => x.DocType == kind);
        if (courseId is long course) query = query.Where(x => x.RelatedCourseId == course);
        if (centerId is long center) query = query.Where(x => x.RelatedCenterId == center);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new VaultRowDto(x.Id, x.FileId, x.DocType, x.TitleAr,
                x.RelatedCourseId, x.RelatedCourse != null ? x.RelatedCourse.CourseNo : null,
                x.RelatedCenterId,
                x.RelatedCenter != null ? x.RelatedCenter.NameAr : null,
                x.UploadedByUser != null && (admin ||
                    x.UploadedByUser.AccountType != AccountType.Employee ||
                    x.UploadedByUser.Employee != null &&
                    x.UploadedByUser.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus)
                    ? x.UploadedByUser.FullNameAr : "—",
                x.UploadedAt))
            .ToListAsync(ct);
        return Result<VaultPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<FileDownloadDto>> GetFileAsync(long actorUserId,
        long documentId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "vault.download", ct))
            return Result<FileDownloadDto>.Failure("FORBIDDEN");
        var document = await db.VaultDocuments.AsNoTracking()
            .Where(x => x.Id == documentId && x.IsActive)
            .Select(x => new { x.FileId, x.TitleAr })
            .SingleOrDefaultAsync(ct);
        if (document is null) return Result<FileDownloadDto>.Failure("VAULT_NOT_FOUND");
        await audit.LogAsync(new AuditEntry(AuditCategory.Document, "vault.download",
            "تنزيل وثيقة من المستودع", "Vault document downloaded", "VaultDocument",
            documentId, document.TitleAr, ActorUserId: actorUserId), ct);
        return await files.DownloadAsync(actorUserId, document.FileId, ct);
    }

    public async Task<Result> DeactivateAsync(long actorUserId, long documentId,
        string reason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "vault.deactivate", ct))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure("REASON_REQUIRED");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return await db.InTransactionAsync(async () =>
        {
            var document = await db.VaultDocuments
                .SingleOrDefaultAsync(x => x.Id == documentId, ct);
            if (document is null) return Result.Failure("VAULT_NOT_FOUND");
            if (!document.IsActive) return Result.Failure("VAULT_ALREADY_DISABLED");
            document.IsActive = false; document.DeactivatedAt = now;
            document.DeactivatedByUserId = actorUserId;
            document.DeactivationReason = reason.Trim();
            await audit.LogAsync(new AuditEntry(AuditCategory.Document, "vault.deactivate",
                "تعطيل وثيقة في المستودع", "Vault document deactivated", "VaultDocument",
                documentId, document.TitleAr,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(
                    new { reason = reason.Trim() }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}
