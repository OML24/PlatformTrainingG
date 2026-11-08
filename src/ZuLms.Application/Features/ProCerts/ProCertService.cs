using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Application.Features.Files;
using ZuLms.Domain.Entities.Files;
using ZuLms.Domain.Entities.Profiles;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.ProCerts;

public sealed class ProCertService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit,
    IFileStorage storage)
    : IProCertService
{
    public async Task<Result<long>> SubmitAsync(long actorUserId, ProCertInput input,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.submit", ct))
            return Result<long>.Failure("FORBIDDEN");
        string? error = await ValidateAsync(actorUserId, input, null, null, ct);
        if (error is not null) return Result<long>.Failure(error);
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            // Repeat attachment authorization in the write transaction to close the
            // validation-to-save race; no records or notifications have been added yet.
            if (!await HasValidAttachmentAsync(actorUserId, input.FileId, null, ct))
                return Result<long>.Failure("PROCERT_FILE_INVALID");
            var cert = new ProfessionalCertification
            {
                UserId = actorUserId,
                CategoryId = input.CategoryId,
                Name = input.Name.Trim(),
                NameAr = Clean(input.NameAr),
                IssuerName = input.IssuerName.Trim(),
                IssuerWebsiteUrl = Clean(input.IssuerWebsiteUrl),
                Description = Clean(input.Description),
                CredentialCode = Clean(input.CredentialCode),
                VerifyUrl = Clean(input.VerifyUrl),
                IssuedDate = input.IssuedDate,
                ExpiryDate = input.ExpiryDate,
                FileId = input.FileId,
                Status = ProCertStatus.Submitted,
                SubmittedAt = now,
                IsActive = true,
                SearchText = BuildSearchText(input),
                CreatedAt = now,
                CreatedByUserId = actorUserId
            };
            db.ProfessionalCertifications.Add(cert);
            await db.SaveChangesAsync(ct);
            await AddEventAsync(cert.Id, "Submitted", null, actorUserId, now, ct);
            await NotifyQualityAsync(cert, "شهادة مهنية جديدة بانتظار المراجعة",
                "New professional certificate submitted", now, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate, "procert.submit",
                "رفع شهادة مهنية", "Professional certificate submitted",
                "ProfessionalCertification", cert.Id, cert.Name,
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(cert.Id);
        }, ct);
    }

    public async Task<Result> WithdrawAsync(long actorUserId, long certId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.withdraw", ct))
            return Result.Failure("FORBIDDEN");
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var cert = await db.ProfessionalCertifications
                .SingleOrDefaultAsync(x => x.Id == certId, ct);
            if (cert is null) return Result.Failure("PROCERT_NOT_FOUND");
            if (cert.UserId != actorUserId) return Result.Failure("FORBIDDEN");
            if (!cert.IsActive) return Result.Failure("PROCERT_ALREADY_DISABLED");
            cert.IsActive = false;
            cert.DeactivatedAt = now; cert.DeactivatedByUserId = actorUserId;
            cert.DeactivationReason = "withdrawn by owner";
            await AddEventAsync(cert.Id, "Withdrawn", null, actorUserId, now, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate, "procert.withdraw",
                "سحب شهادة مهنية", "Professional certificate withdrawn",
                "ProfessionalCertification", cert.Id, cert.Name,
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> EditAsync(long actorUserId, long certId,
        ProCertInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.edit", ct))
            return Result.Failure("FORBIDDEN");
        return await ApplyEditAsync(actorUserId, certId, input, resubmit: false, ct);
    }

    public async Task<Result> ResubmitAsync(long actorUserId, long certId,
        ProCertInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.submit", ct))
            return Result.Failure("FORBIDDEN");
        var cert = await db.ProfessionalCertifications.AsNoTracking()
            .Where(x => x.Id == certId)
            .Select(x => new { x.UserId, x.Status }).SingleOrDefaultAsync(ct);
        if (cert is null) return Result.Failure("PROCERT_NOT_FOUND");
        if (cert.UserId != actorUserId || cert.Status != ProCertStatus.Rejected)
            return Result.Failure("FORBIDDEN");
        return await ApplyEditAsync(actorUserId, certId, input, resubmit: true, ct);
    }

    public async Task<Result> DecideAsync(long actorUserId, long certId, bool approve,
        string? notes, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.review", ct))
            return Result.Failure("FORBIDDEN");
        if (!approve && string.IsNullOrWhiteSpace(notes))
            return Result.Failure("REASON_REQUIRED");
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var cert = await db.ProfessionalCertifications
                .SingleOrDefaultAsync(x => x.Id == certId, ct);
            if (cert is null) return Result.Failure("PROCERT_NOT_FOUND");
            if (cert.Status != ProCertStatus.Submitted)
                return Result.Failure("PROCERT_NOT_PENDING");
            cert.Status = approve ? ProCertStatus.Approved : ProCertStatus.Rejected;
            cert.DecidedBy = actorUserId; cert.DecidedAt = now;
            cert.DecisionNotes = approve ? Clean(notes) : notes!.Trim();
            cert.UpdatedAt = now; cert.UpdatedByUserId = actorUserId;
            await AddEventAsync(cert.Id, approve ? "Approved" : "Rejected",
                cert.DecisionNotes, actorUserId, now, ct);
            db.Notifications.Add(new Domain.Entities.Notify.Notification
            {
                UserId = cert.UserId,
                Type = approve ? NotificationType.ProCertApproved
                    : NotificationType.ProCertRejected,
                TitleAr = approve ? "اعتماد شهادة مهنية" : "رفض شهادة مهنية",
                TitleEn = approve ? "Professional certificate approved"
                    : "Professional certificate rejected",
                BodyAr = approve ? cert.Name : $"{cert.Name} — {notes!.Trim()}",
                BodyEn = cert.Name,
                LinkUrl = "/member/pro-certs",
                RelatedEntityType = "ProfessionalCertification",
                RelatedEntityId = cert.Id,
                CreatedAt = now,
                IsActive = true
            });
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
                approve ? "procert.approve" : "procert.reject",
                approve ? "اعتماد شهادة مهنية" : "رفض شهادة مهنية",
                approve ? "Professional certificate approved" : "Professional certificate rejected",
                "ProfessionalCertification", certId, cert.Name,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(new { approve }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> DeactivateAsync(long actorUserId, long certId,
        string reason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.deactivate", ct))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure("REASON_REQUIRED");
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var cert = await db.ProfessionalCertifications
                .SingleOrDefaultAsync(x => x.Id == certId, ct);
            if (cert is null) return Result.Failure("PROCERT_NOT_FOUND");
            if (!cert.IsActive) return Result.Failure("PROCERT_ALREADY_DISABLED");
            cert.IsActive = false; cert.DeactivatedAt = now;
            cert.DeactivatedByUserId = actorUserId; cert.DeactivationReason = reason.Trim();
            await AddEventAsync(cert.Id, "Deactivated", reason.Trim(), actorUserId, now, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate, "procert.deactivate",
                "تعطيل شهادة مهنية", "Professional certificate deactivated",
                "ProfessionalCertification", certId, cert.Name,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(
                    new { reason = reason.Trim() }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result<ProCertPageDto>> ListAsync(long actorUserId,
        string? search, ProCertStatus? status, long? categoryId, bool includeInactive,
        int page, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool reviewer = (effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality)) &&
            effective.Keys.Contains("page.quality.pro_certs");
        bool member = ScopeGuard.AllowsPersonalRead(effective, "procert.submit");
        if (!reviewer && !member) return Result<ProCertPageDto>.Failure("FORBIDDEN");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1) page = 1;
        string term = ArabicSearchNormalizer.Normalize(search);
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var query = db.ProfessionalCertifications.AsNoTracking();
        if (!reviewer) query = query.Where(x => x.UserId == actorUserId);
        if (!effective.HasMode(RoleCode.Admin))
            query = query.Where(x => x.User!.AccountType != AccountType.Employee ||
                x.User.Employee != null && x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (term.Length > 0)
            query = query.Where(x => x.SearchText.Contains(term) ||
                x.User!.FullNameAr.Contains(term));
        if (status is { } value) query = query.Where(x => x.Status == value);
        if (categoryId is long category) query = query.Where(x => x.CategoryId == category);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProCertRowDto(x.Id, x.UserId, x.User!.FullNameAr,
                x.Name, x.IssuerName, x.CategoryId, x.Category!.NameAr, x.Status,
                x.IssuedDate, x.ExpiryDate, x.ExpiryDate.HasValue &&
                    x.ExpiryDate.Value < today, x.CredentialCode, x.VerifyUrl,
                x.FileId, x.DecisionNotes, x.SubmittedAt))
            .ToListAsync(ct);
        return Result<ProCertPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<ProCertDetailsDto>> GetAsync(long actorUserId, long certId,
        CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool reviewer = (effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality)) &&
            effective.Keys.Contains("page.quality.pro_certs");
        bool member = ScopeGuard.AllowsPersonalRead(effective, "procert.submit");
        if (!reviewer && !member) return Result<ProCertDetailsDto>.Failure("FORBIDDEN");
        bool admin = effective.HasMode(RoleCode.Admin);
        var row = await db.ProfessionalCertifications.AsNoTracking()
            .Where(x => x.Id == certId && (admin ||
                x.User!.AccountType != AccountType.Employee || x.User.Employee != null &&
                x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
            .Select(x => new ProCertRowDto(x.Id, x.UserId, x.User!.FullNameAr,
                x.Name, x.IssuerName, x.CategoryId, x.Category!.NameAr, x.Status,
                x.IssuedDate, x.ExpiryDate, x.ExpiryDate.HasValue &&
                    x.ExpiryDate.Value < DateOnly.FromDateTime(DateTime.UtcNow),
                x.CredentialCode, x.VerifyUrl, x.FileId, x.DecisionNotes, x.SubmittedAt))
            .SingleOrDefaultAsync(ct);
        if (row is null) return Result<ProCertDetailsDto>.Failure("PROCERT_NOT_FOUND");
        if (!reviewer && (!member || row.UserId != actorUserId))
            return Result<ProCertDetailsDto>.Failure("FORBIDDEN");
        var events = await db.ProfessionalCertEvents.AsNoTracking()
            .Where(x => x.CertId == certId).OrderBy(x => x.At).ThenBy(x => x.Id)
            .Select(x => $"{x.Event} — {x.At:yyyy/MM/dd HH:mm} — {x.ByUser!.FullNameAr}" +
                (string.IsNullOrWhiteSpace(x.Notes) ? string.Empty : $" — {x.Notes}"))
            .ToListAsync(ct);
        return Result<ProCertDetailsDto>.Success(new(row, events));
    }

    public async Task<Result<FileDownloadDto>> GetFileAsync(long actorUserId, long certId,
        CancellationToken ct)
    {
        var cert = await db.ProfessionalCertifications.AsNoTracking()
            .Where(x => x.Id == certId && x.IsActive && x.File!.IsActive)
            .Select(x => new { x.UserId, x.File!.RelativePath, x.File.OriginalName, x.File.MimeType, x.Name,
                VisibleOwner = x.User!.AccountType != AccountType.Employee ||
                    x.User.Employee != null &&
                    x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus })
            .SingleOrDefaultAsync(ct);
        if (cert is null) return Result<FileDownloadDto>.Failure("PROCERT_NOT_FOUND");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool owner = cert.UserId == actorUserId &&
            FileDownloadAccessPolicy.AllowsPersonalDownload(FileKind.ProfessionalCertificate, effective);
        bool any = FileDownloadAccessPolicy.AllowsProCertReviewerDownload(effective);
        if (!effective.HasMode(RoleCode.Admin) && !cert.VisibleOwner)
            return Result<FileDownloadDto>.Failure("FORBIDDEN");
        if (!owner && !any) return Result<FileDownloadDto>.Failure("FORBIDDEN");
        Stream stream;
        try { stream = await storage.OpenReadAsync(cert.RelativePath, ct); }
        catch (FileNotFoundException)
        {
            return Result<FileDownloadDto>.Failure("FILE_CONTENT_MISSING");
        }
        try
        {
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate, "procert.download",
                "تنزيل ملف شهادة مهنية", "Professional certificate file downloaded",
                "ProfessionalCertification", certId, cert.Name, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
        }
        catch { await stream.DisposeAsync(); throw; }
        return Result<FileDownloadDto>.Success(new(stream, cert.OriginalName, cert.MimeType));
    }

    public async Task<Result<IReadOnlyList<ProCertCategoryDto>>> CategoriesAsync(
        CancellationToken ct)
    {
        var rows = await db.ProfessionalCertCategorys.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new ProCertCategoryDto(x.Id, x.NameAr, x.NameEn, x.SortOrder))
            .ToListAsync(ct);
        return Result<IReadOnlyList<ProCertCategoryDto>>.Success(rows);
    }

    public async Task<Result> SaveCategoryAsync(long actorUserId, long? categoryId,
        string nameAr, string? nameEn, int sortOrder, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.categories.manage", ct))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Trim().Length > 200)
            return Result.Failure("CATEGORY_INVALID");
        DateTimeOffset now = clock.UtcNow;
        return await db.InTransactionAsync(async () =>
        {
            if (categoryId is long id)
            {
                var category = await db.ProfessionalCertCategorys
                    .SingleOrDefaultAsync(x => x.Id == id, ct);
                if (category is null) return Result.Failure("CATEGORY_NOT_FOUND");
                category.NameAr = nameAr.Trim(); category.NameEn = Clean(nameEn);
                category.SortOrder = sortOrder;
                category.UpdatedAt = now; category.UpdatedByUserId = actorUserId;
            }
            else
            {
                db.ProfessionalCertCategorys.Add(new ProfessionalCertCategory
                {
                    NameAr = nameAr.Trim(),
                    NameEn = Clean(nameEn),
                    SortOrder = sortOrder,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedByUserId = actorUserId
                });
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "procert.category_save",
                "حفظ تصنيف شهادة مهنية", "Professional certificate category saved",
                "ProfessionalCertCategory", categoryId ?? 0, nameAr.Trim(),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result<ProCertStatsDto>> StatsAsync(long actorUserId,
        long? facultyId, long? categoryId, string? issuer, DateOnly? from, DateOnly? to,
        bool includeExpired, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "procert.stats", ct))
            return Result<ProCertStatsDto>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        int topN = await settings.GetAsync<int>("procert.stats_top_n", ct);
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var query = db.ProfessionalCertifications.AsNoTracking()
            .Where(x => x.Status == ProCertStatus.Approved && x.IsActive);
        if (!admin) query = query.Where(x => x.User!.AccountType != AccountType.Employee ||
            x.User.Employee != null && x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus);
        if (!includeExpired)
            query = query.Where(x => x.ExpiryDate == null || x.ExpiryDate >= today);
        if (categoryId is long category) query = query.Where(x => x.CategoryId == category);
        if (!string.IsNullOrWhiteSpace(issuer))
        {
            string raw = issuer.Trim();
            query = query.Where(x => x.IssuerName.Contains(raw));
        }
        if (from is { } fromDate) query = query.Where(x => x.IssuedDate >= fromDate);
        if (to is { } toDate) query = query.Where(x => x.IssuedDate <= toDate);
        if (facultyId == 0)
            query = query.Where(x => x.User!.Employee != null &&
                !x.User.Employee.IsAcademic);
        else if (facultyId is long faculty)
            query = query.Where(x => x.User!.Employee!.CenterId == faculty);

        var rows = await query.Select(x => new
        {
            x.UserId,
            OwnerName = x.User!.FullNameAr,
            CenterId = x.User.Employee != null ? (long?)x.User.Employee.CenterId : null,
            IsFaculty = x.User.Employee != null && x.User.Employee.IsAcademic,
            FacultyName = x.User.Employee != null && !x.User.Employee.IsAcademic
                ? "الإداريون" : x.User.Employee != null && x.User.Employee.Center != null
                    ? x.User.Employee.Center.NameAr : null,
            x.CategoryId,
            CategoryName = x.Category!.NameAr,
            x.IssuerName,
            x.IssuedDate
        }).ToListAsync(ct);

        int certs = rows.Count;
        var byOwner = rows.GroupBy(x => x.UserId).ToList();
        int members = byOwner.Count;
        int activeMembers = await db.Users.AsNoTracking()
            .CountAsync(x => x.IsActive && x.AccountType == AccountType.Employee &&
                (admin || x.Employee != null && x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus), ct);
        double avg = members == 0 ? 0 : Math.Round(certs * 100.0 / members, 1) / 100.0;
        double coverage = activeMembers == 0 ? 0
            : Math.Round(members * 100.0 / activeMembers, 1);
        var totals = new ProCertStatsTotalsDto(certs, members, avg, members, coverage);

        var topMembers = byOwner
            .Select(g => new ProCertTopMemberDto(g.Key, g.First().OwnerName,
                !string.IsNullOrEmpty(g.First().FacultyName)
                    ? g.First().FacultyName! : "جهات غير أكاديمية", g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).Take(topN).ToList();

        var facultyGroups = rows.Where(x => x.CenterId != null || !x.IsFaculty)
            .GroupBy(x => x.IsFaculty ? x.CenterId!.Value : 0).ToList();
        var byFaculty = facultyGroups
            .Select(g => new ProCertFacultyDto(g.Key,
                g.First().FacultyName ?? "—", g.Count(),
                g.Select(x => x.UserId).Distinct().Count(),
                Math.Round(g.Count() * 100.0 / g.Select(x => x.UserId).Distinct().Count(), 1) / 100.0))
            .OrderByDescending(x => x.Certs).ToList();
        var topByFaculty = facultyGroups
            .Select(g => new ProCertFacultyTopDto(g.Key, g.First().FacultyName ?? "—",
                g.GroupBy(x => x.UserId)
                    .Select(u => new ProCertTopMemberDto(u.Key, u.First().OwnerName,
                        g.First().FacultyName ?? "—", u.Count()))
                    .OrderByDescending(u => u.Count).ThenBy(u => u.Name).Take(topN).ToList()))
            .OrderByDescending(x => x.Members.Sum(m => m.Count)).ToList();
        var byCategory = rows.GroupBy(x => x.CategoryId)
            .Select(g => new ProCertCategoryStatDto(g.Key, g.First().CategoryName,
                g.Count(), g.Select(x => x.UserId).Distinct().Count()))
            .OrderByDescending(x => x.Certs).ToList();
        var topIssuers = rows.GroupBy(x => x.IssuerName)
            .Select(g => new ProCertIssuerDto(g.Key, g.Count(),
                g.Select(x => x.UserId).Distinct().Count()))
            .OrderByDescending(x => x.Certs).Take(topN).ToList();
        var byYear = rows.GroupBy(x => x.IssuedDate.Year)
            .Select(g => new ProCertYearDto(g.Key, g.Count()))
            .OrderBy(x => x.Year).ToList();

        return Result<ProCertStatsDto>.Success(new ProCertStatsDto(totals, topMembers,
            topByFaculty, byFaculty, byCategory, topIssuers, byYear));
    }

    private async Task<Result> ApplyEditAsync(long actorUserId, long certId,
        ProCertInput input, bool resubmit, CancellationToken ct)
    {
        var current = await db.ProfessionalCertifications.AsNoTracking()
            .Where(x => x.Id == certId)
            .Select(x => new { x.UserId, x.Status, x.FileId })
            .SingleOrDefaultAsync(ct);
        if (current is null) return Result.Failure("PROCERT_NOT_FOUND");
        if (resubmit && (current.UserId != actorUserId ||
            current.Status != ProCertStatus.Rejected))
            return Result.Failure("FORBIDDEN");
        string? error = await ValidateAsync(actorUserId, input, certId, current.FileId, ct);
        if (error is not null) return Result.Failure(error);
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var cert = await db.ProfessionalCertifications
                .SingleOrDefaultAsync(x => x.Id == certId, ct);
            if (cert is null) return Result.Failure("PROCERT_NOT_FOUND");
            if (resubmit && (cert.UserId != actorUserId ||
                cert.Status != ProCertStatus.Rejected))
                return Result.Failure("FORBIDDEN");
            if (!await HasValidAttachmentAsync(actorUserId, input.FileId,
                cert.FileId, ct))
                return Result.Failure("PROCERT_FILE_INVALID");
            cert.Name = input.Name.Trim(); cert.NameAr = Clean(input.NameAr);
            cert.IssuerName = input.IssuerName.Trim();
            cert.IssuerWebsiteUrl = Clean(input.IssuerWebsiteUrl);
            cert.Description = Clean(input.Description);
            cert.CredentialCode = Clean(input.CredentialCode);
            cert.VerifyUrl = Clean(input.VerifyUrl);
            cert.CategoryId = input.CategoryId;
            cert.IssuedDate = input.IssuedDate; cert.ExpiryDate = input.ExpiryDate;
            cert.FileId = input.FileId;
            cert.SearchText = BuildSearchText(input);
            cert.UpdatedAt = now; cert.UpdatedByUserId = actorUserId;
            if (resubmit)
            {
                cert.Status = ProCertStatus.Submitted;
                cert.DecisionNotes = null;
                await AddEventAsync(cert.Id, "Resubmitted", null, actorUserId, now, ct);
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
                resubmit ? "procert.resubmit" : "procert.edit",
                resubmit ? "إعادة إرسال شهادة مهنية" : "تعديل شهادة مهنية",
                resubmit ? "Professional certificate resubmitted" : "Professional certificate edited",
                "ProfessionalCertification", certId, cert.Name, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<string?> ValidateAsync(long actorUserId, ProCertInput input,
        long? editingId, long? currentFileId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(input.IssuerName) ||
            input.IssuerName.Trim().Length > 300 ||
            !await db.ProfessionalCertCategorys.AnyAsync(x =>
                x.Id == input.CategoryId && x.IsActive, ct))
            return "PROCERT_INVALID";
        if (string.IsNullOrWhiteSpace(input.CredentialCode) &&
            string.IsNullOrWhiteSpace(input.VerifyUrl))
            return "PROCERT_NEEDS_EVIDENCE";
        if (input.IssuerWebsiteUrl is not null)
        {
            if (!Uri.TryCreate(input.IssuerWebsiteUrl, UriKind.Absolute, out Uri? site) ||
                site!.Scheme != "https")
                return "PROCERT_URL_INVALID";
        }
        if (input.VerifyUrl is not null)
        {
            if (!Uri.TryCreate(input.VerifyUrl, UriKind.Absolute, out Uri? verify) ||
                verify!.Scheme is not "https" and not "http")
                return "PROCERT_URL_INVALID";
        }
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if (input.IssuedDate > today) return "PROCERT_DATE_INVALID";
        if (input.ExpiryDate is { } expiry && expiry < input.IssuedDate)
            return "PROCERT_DATE_INVALID";
        if (!await HasValidAttachmentAsync(actorUserId, input.FileId,
            currentFileId, ct))
            return "PROCERT_FILE_INVALID";
        string? code = Clean(input.CredentialCode);
        if (code is not null)
        {
            bool duplicate = await db.ProfessionalCertifications.AsNoTracking()
                .AnyAsync(x => x.UserId == actorUserId && x.IssuerName == input.IssuerName.Trim() &&
                    x.CredentialCode == code && x.Status != ProCertStatus.Rejected &&
                    x.IsActive && (editingId == null || x.Id != editingId), ct);
            if (duplicate) return "PROCERT_DUPLICATE";
        }
        return null;
    }

    private async Task<bool> HasValidAttachmentAsync(long actorUserId,
        long requestedFileId, long? currentFileId, CancellationToken ct)
    {
        var file = await db.StoredFiles.AsNoTracking()
            .Where(x => x.Id == requestedFileId && x.IsActive)
            .Select(x => new { x.Kind, x.UploadedBy }).SingleOrDefaultAsync(ct);
        return ProCertAttachmentPolicy.Allows(requestedFileId, currentFileId,
            file is not null, file?.Kind, file?.UploadedBy, actorUserId);
    }

    private async Task AddEventAsync(long certId, string @event, string? notes,
        long byUserId, DateTimeOffset at, CancellationToken ct)
    {
        db.ProfessionalCertEvents.Add(new ProfessionalCertEvent
        {
            CertId = certId,
            Event = @event,
            Notes = Clean(notes),
            ByUserId = byUserId,
            At = at
        });
        await Task.CompletedTask;
    }

    private async Task NotifyQualityAsync(ProfessionalCertification cert, string titleAr,
        string titleEn, DateTimeOffset now, CancellationToken ct)
    {
        var qualityUsers = await db.UserRoles.AsNoTracking()
            .Where(x => x.IsActive && x.Role!.Code == RoleCode.Quality && x.User!.IsActive)
            .Select(x => x.UserId).Distinct().ToListAsync(ct);
        foreach (long userId in qualityUsers)
            db.Notifications.Add(new Domain.Entities.Notify.Notification
            {
                UserId = userId,
                Type = NotificationType.ProCertSubmitted,
                TitleAr = titleAr,
                TitleEn = titleEn,
                BodyAr = cert.Name,
                BodyEn = cert.Name,
                LinkUrl = "/quality/pro-certs",
                RelatedEntityType = "ProfessionalCertification",
                RelatedEntityId = cert.Id,
                CreatedAt = now,
                IsActive = true
            });
    }

    private static string BuildSearchText(ProCertInput input) => string.Join(' ',
        new[] { input.Name.Trim(), Clean(input.NameAr), input.IssuerName.Trim(),
            Clean(input.CredentialCode), Clean(input.VerifyUrl) }
        .Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
