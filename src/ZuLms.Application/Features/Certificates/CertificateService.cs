using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Application.Features.Files;
using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Entities.Files;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Certificates;

public sealed class CertificateService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings,
    ICertificatePdfRenderer renderer, IQrCodeService qr, IFileStorage storage,
    IEligibilitySnapshotService eligibility, IAuditLogger audit) : ICertificateService
{
    private const int CurrentPdfRenderVersion = 2;
    public async Task<Result<CertificateIssueDto>> IssueAsync(long courseId,
        long participantId, long? actorUserId, CancellationToken ct)
    {
        var course = await db.Courses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => new
            {
                x.Id,
                x.TitleAr,
                x.TitleEn,
                x.TotalHours,
                x.Origin,
                x.PlannedStartDate,
                x.PlannedEndDate,
                x.ProposingCenterId,
                x.TargetCenterId,
                x.AudienceType,
                x.InstructorNameAr,
                x.InstructorNameEn,
                x.ManagedByQuality,
                ProposingCode = x.ProposingCenter != null ? x.ProposingCenter.ShortCode : null,
                TargetCode = x.TargetCenter != null ? x.TargetCenter.ShortCode : null
            }).SingleOrDefaultAsync(ct);
        if (course is null) return Result<CertificateIssueDto>.Failure("COURSE_NOT_FOUND");
        var participant = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.Id == participantId && x.CourseId == courseId)
            .Select(x => new { x.Id, x.UserId }).SingleOrDefaultAsync(ct);
        if (participant is null)
            return Result<CertificateIssueDto>.Failure("PARTICIPANT_NOT_FOUND");
        bool alreadyIssued = await db.Certificates.IgnoreQueryFilters()
            .AnyAsync(x => x.ParticipantId == participantId &&
                x.Status == CertificateStatus.Issued, ct);
        if (alreadyIssued)
            return Result<CertificateIssueDto>.Failure("CERTIFICATE_ALREADY_ISSUED");
        var holder = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == participant.UserId)
            .Select(x => new { x.FullNameAr, x.FullNameEn }).SingleOrDefaultAsync(ct);
        if (holder is null)
            return Result<CertificateIssueDto>.Failure("USER_NOT_FOUND");
        var template = await db.CertificateTemplates.AsNoTracking()
            .Where(x => x.IsCurrent)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new { x.Id, x.FileId, x.PageWidthPt, x.PageHeightPt, x.LayoutJson })
            .FirstOrDefaultAsync(ct);
        if (template is null)
            return Result<CertificateIssueDto>.Failure("CERT_TEMPLATE_MISSING");

        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset ammanNow = TimeZoneInfo.ConvertTime(now, clock.Amman);
        int year = ammanNow.Year;
        string generalCode = await settings.GetAsync<string>("cert.general_short_code", ct);
        string? facultyCode = course.Origin == CourseOrigin.DeanProposal
            ? course.ProposingCode
            : course.AudienceType == AudienceType.Faculty ? course.TargetCode : null;
        string code = (facultyCode ?? string.Empty).Trim();
        if (code.Length == 0) code = generalCode.Trim();
        if (code.Length == 0) code = year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string pattern = await settings.GetAsync<string>("cert.reference_pattern", ct);
        int padding = await settings.GetAsync<int>("cert.reference_padding", ct);
        long seq = await db.NextSequenceAsync($"CERT:{year}:{code}", ct);
        string referenceNo = CertificateReference.Format(
            pattern.Length == 0 ? "ZU-{YYYY}-{CODE}-{NNNN}" : pattern,
            year, code, checked((int)seq), padding <= 0 ? 4 : padding);
        string verificationToken = Base64UrlToken(32);

        var bounds = await db.Lectures.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive)
            .Select(x => x.StartsAt).ToListAsync(ct);
        string periodText = bounds.Count == 0
            ? course.PlannedStartDate is { } start && course.PlannedEndDate is { } end
                ? $"{start:dd/MM/yyyy} – {end:dd/MM/yyyy}"
                : "—"
            : $"{TimeZoneInfo.ConvertTime(bounds.Min(), clock.Amman):dd/MM/yyyy} – {TimeZoneInfo.ConvertTime(bounds.Max(), clock.Amman):dd/MM/yyyy}";

        string verifyBase = await CertificateVerificationUrl.BaseAsync(settings, ct);
        string verifyUrl = CertificateVerificationUrl.Build(verifyBase, referenceNo);
        byte[] qrPng = qr.GeneratePng(verifyUrl, 600);
        byte[] templatePdf;
        var templateFile = await db.StoredFiles.AsNoTracking()
            .Where(x => x.Id == template.FileId && x.IsActive)
            .Select(x => new { x.RelativePath }).SingleOrDefaultAsync(ct);
        if (templateFile is null)
            return Result<CertificateIssueDto>.Failure("CERT_TEMPLATE_MISSING");
        try
        {
            await using (var stream = await storage.OpenReadAsync(templateFile.RelativePath, ct))
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer, ct);
                templatePdf = buffer.ToArray();
            }
        }
        catch (FileNotFoundException)
        {
            return Result<CertificateIssueDto>.Failure("CERT_TEMPLATE_MISSING");
        }

        var layer = new CertificateTextLayerInput(template.PageWidthPt, template.PageHeightPt,
            template.LayoutJson, holder.FullNameAr, holder.FullNameEn,
            course.TitleAr, course.TitleEn, periodText,
            FormattableString.Invariant($"{course.TotalHours:0.##}"),
            course.InstructorNameAr, course.InstructorNameEn,
            ammanNow.ToString("dd/MM/yyyy"), referenceNo, verifyUrl);
        byte[] pdf;
        try { pdf = renderer.Render(templatePdf, layer, qrPng); }
        catch (Exception) { return Result<CertificateIssueDto>.Failure("CERT_RENDERER_FAILED"); }

        string relative = await storage.WriteAsync(".pdf", pdf, now, ct);
        var pdfFile = new StoredFile
        {
            Kind = FileKind.CertificatePdf,
            OriginalName = $"{referenceNo}.pdf",
            StoredName = Path.GetFileName(relative),
            RelativePath = relative,
            MimeType = "application/pdf",
            SizeBytes = pdf.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(),
            UploadedBy = actorUserId,
            UploadedAt = now,
            IsActive = true
        };
        db.StoredFiles.Add(pdfFile);
        await db.SaveChangesAsync(ct);

        var certificate = new Domain.Entities.Certificates.Certificate
        {
            ReferenceNo = referenceNo,
            VerificationToken = verificationToken,
            CourseId = courseId,
            ParticipantId = participantId,
            UserId = participant.UserId,
            Status = CertificateStatus.Issued,
            IssuedAt = now,
            TemplateId = template.Id,
            PdfFileId = pdfFile.Id,
            HolderNameAr = holder.FullNameAr,
            HolderNameEn = holder.FullNameEn,
            CourseTitleAr = course.TitleAr,
            CourseTitleEn = course.TitleEn,
            CoursePeriodText = periodText,
            CourseHours = course.TotalHours,
            InstructorNameAr = course.InstructorNameAr,
            InstructorNameEn = course.InstructorNameEn,
            PdfRenderVersion = CurrentPdfRenderVersion,
            IsActive = true,
            HiddenByCourseDisable = false,
            CreatedAt = now,
            CreatedByUserId = actorUserId
        };
        db.Certificates.Add(certificate);
        await db.SaveChangesAsync(ct);
        db.Notifications.Add(new Notification
        {
            UserId = participant.UserId,
            Type = NotificationType.CertificateIssued,
            TitleAr = "إصدار شهادة دورة",
            TitleEn = "Course certificate issued",
            BodyAr = course.TitleAr,
            BodyEn = course.TitleEn ?? course.TitleAr,
            LinkUrl = $"/member/certificates",
            RelatedEntityType = "Certificate",
            RelatedEntityId = certificate.Id,
            CreatedAt = now,
            IsActive = true
        });
        await audit.LogAsync(new AuditEntry(AuditCategory.Certificate, "certificate.issue",
            "إصدار شهادة", "Certificate issued", "Certificate", certificate.Id, referenceNo,
            CourseId: courseId,
            AfterJson: System.Text.Json.JsonSerializer.Serialize(new { referenceNo, participantId }),
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result<CertificateIssueDto>.Success(new(certificate.Id, referenceNo));
    }

    public async Task<Result> RevokeAsync(long actorUserId, long certificateId,
        string reason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "certificates.revoke_manual", ct))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure("REASON_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var certificate = await db.Certificates.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == certificateId, ct);
            if (certificate is null) return Result.Failure("CERT_NOT_FOUND");
            if (certificate.Status == CertificateStatus.Revoked)
                return Result.Failure("CERT_ALREADY_REVOKED");
            DateTimeOffset now = clock.UtcNow;
            certificate.Status = CertificateStatus.Revoked;
            certificate.RevokedReason = CertificateRevokeReason.ManualByAdmin;
            certificate.RevokedAt = now;
            certificate.RevokedBy = actorUserId;
            certificate.RevokedNote = reason.Trim();
            db.Notifications.Add(new Notification
            {
                UserId = certificate.UserId,
                Type = NotificationType.CertificateRevoked,
                TitleAr = "إلغاء شهادة",
                TitleEn = "Certificate revoked",
                BodyAr = $"{certificate.CourseTitleAr} — {reason.Trim()}",
                BodyEn = $"{certificate.CourseTitleEn ?? certificate.CourseTitleAr} — {reason.Trim()}",
                LinkUrl = "/member/certificates",
                RelatedEntityType = "Certificate",
                RelatedEntityId = certificate.Id,
                CreatedAt = now,
                IsActive = true
            });
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate, "certificate.revoke",
                "إلغاء شهادة يدوياً", "Certificate revoked manually", "Certificate",
                certificate.Id, certificate.ReferenceNo, CourseId: certificate.CourseId,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(new { reason = reason.Trim() }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result<int>> IssuePendingAsync(long actorUserId, long? courseId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "certificates.issue_pending", ct))
            return Result<int>.Failure("FORBIDDEN");
        var query = db.CourseParticipants.AsNoTracking()
            .Where(x => x.IsActive && x.Course!.Status == CourseStatus.Finished &&
                x.Course.IsActive);
        if (courseId is long only) query = query.Where(x => x.CourseId == only);
        var participants = await query.Select(x => new { x.Id, x.CourseId }).ToListAsync(ct);
        int issued = 0;
        foreach (var participant in participants)
        {
            var state = await eligibility.EvaluateAsync(participant.CourseId, participant.Id,
                false, ct);
            if (!state.Ok || !state.Value!.Eligible) continue;
            bool hasCertificate = await db.Certificates.IgnoreQueryFilters()
                .AnyAsync(x => x.ParticipantId == participant.Id &&
                    x.Status == CertificateStatus.Issued, ct);
            if (hasCertificate) continue;
            var result = await db.InSerializableTransactionAsync(() =>
                IssueAsync(participant.CourseId, participant.Id, actorUserId, ct), ct);
            if (result.Ok) issued++;
        }
        return Result<int>.Success(issued);
    }

    public async Task<Result<AdministrativeCertificateResultDto>> ForceIssueAsync(
        long actorUserId, long courseId, int employeeCode, bool evaluationExempt,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "certificates.issue_pending", ct))
            return Result<AdministrativeCertificateResultDto>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!effective.HasMode(RoleCode.Admin))
            return Result<AdministrativeCertificateResultDto>.Failure("FORBIDDEN");
        if (courseId <= 0 || employeeCode <= 0)
            return Result<AdministrativeCertificateResultDto>.Failure("REQUEST_INVALID");
        var matchingUsers = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsActive && x.AccountType == AccountType.Employee &&
                x.Employee != null && x.Employee.EmployeeCode == employeeCode)
            .Select(x => x.Id).Take(2).ToListAsync(ct);
        if (matchingUsers.Count == 0)
            return Result<AdministrativeCertificateResultDto>.Failure("USER_NOT_FOUND");
        if (matchingUsers.Count > 1)
            return Result<AdministrativeCertificateResultDto>.Failure("EMPLOYEE_CODE_AMBIGUOUS");
        long userId = matchingUsers[0];
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null)
                return Result<AdministrativeCertificateResultDto>.Failure("COURSE_NOT_FOUND");
            var participant = await db.CourseParticipants.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.CourseId == courseId && x.UserId == userId, ct);
            DateTimeOffset now = clock.UtcNow;
            if (participant is null)
            {
                participant = new CourseParticipant
                {
                    CourseId = courseId,
                    UserId = userId,
                    Source = ParticipantSource.AddedByAdmin,
                    AddedByUserId = actorUserId,
                    JoinedAt = now,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedByUserId = actorUserId
                };
                db.CourseParticipants.Add(participant);
                await db.SaveChangesAsync(ct);
            }
            else if (!participant.IsActive)
            {
                participant.IsActive = true; participant.RemovedAt = null;
                participant.RemovedBy = null; participant.RemovalReason = null;
            }
            participant.AdminCertificateOverride = true;
            participant.EvaluationExempt = evaluationExempt;
            participant.Notes = AppendAdministrativeCompletionNote(participant.Notes,
                actorUserId, now);
            participant.UpdatedAt = now; participant.UpdatedByUserId = actorUserId;
            await db.SaveChangesAsync(ct);
            var existing = await db.Certificates.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.ParticipantId == participant.Id &&
                    x.Status == CertificateStatus.Issued)
                .Select(x => new { x.Id, x.ReferenceNo }).SingleOrDefaultAsync(ct);
            if (existing is not null)
                return Result<AdministrativeCertificateResultDto>.Success(new(
                    true, false, existing.Id, existing.ReferenceNo));
            var state = await eligibility.EvaluateAsync(courseId, participant.Id, false, ct);
            if (!state.Ok)
                return Result<AdministrativeCertificateResultDto>.Failure(state.Error!.Code);
            if (!state.Value!.Eligible)
            {
                await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
                    "certificate.force_completion_pending_evaluation",
                    "تسجيل استيفاء إداري بانتظار التقييم",
                    "Administrative completion pending evaluation", "CourseParticipant",
                    participant.Id, course.TitleAr, CourseId: courseId,
                    AfterJson: System.Text.Json.JsonSerializer.Serialize(new
                    { employeeCode, userId, evaluationExempt = false, completionRecorded = true }),
                    ActorUserId: actorUserId), ct);
                await db.SaveChangesAsync(ct);
                return Result<AdministrativeCertificateResultDto>.Success(new(
                    false, true, null, null));
            }
            var issued = await IssueAsync(courseId, participant.Id, actorUserId, ct);
            if (!issued.Ok)
                return Result<AdministrativeCertificateResultDto>.Failure(issued.Error!.Code);
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
                "certificate.force_issue", "إصدار شهادة استثنائية من المدير",
                "Certificate force-issued by admin", "Certificate", issued.Value!.Id,
                issued.Value.ReferenceNo, CourseId: courseId,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    employeeCode,
                    userId,
                    evaluationExempt,
                    completionRecorded = true,
                    attendanceOverridden = true,
                    examOverridden = true,
                    impactOverridden = true
                }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<AdministrativeCertificateResultDto>.Success(new(
                true, false, issued.Value!.Id, issued.Value.ReferenceNo));
        }, ct);
    }

    public async Task<Result<CertificatePageDto>> ListAsync(long actorUserId,
        string? search, long? courseId, CertificateStatus? status, int page,
        CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool all = (effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality)) &&
            effective.Keys.Contains("certificates.view.all");
        bool faculty = !all && effective.IsUnitHead &&
            effective.Keys.Contains("certificates.view.faculty");
        if (!all && !faculty) return Result<CertificatePageDto>.Failure("FORBIDDEN");
        var scope = all ? null : effective.DeanScopeCenterIds.Cast<long?>().ToList();
        if (!all && scope!.Count == 0)
            return Result<CertificatePageDto>.Failure("FORBIDDEN");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1 || pageSize < 1) page = 1;
        string term = ArabicSearchNormalizer.Normalize(search);
        var query = db.Certificates.IgnoreQueryFilters().AsNoTracking();
        if (!effective.HasMode(RoleCode.Admin))
            query = query.Where(x => x.User != null && x.User.Employee != null &&
                x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus);
        if (!all)
            query = query.Where(x => scope!.Contains(x.Course!.ProposingCenterId) ||
                scope!.Contains(x.Course!.TargetCenterId));
        if (term.Length > 0)
            query = query.Where(x => x.ReferenceNo.Contains(term) ||
                x.HolderNameAr.Contains(term) || x.CourseTitleAr.Contains(term));
        if (courseId is long id) query = query.Where(x => x.CourseId == id);
        if (status is { } value) query = query.Where(x => x.Status == value);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CertificateRowDto(x.Id, x.ReferenceNo, x.CourseId,
                x.Course!.CourseNo, x.CourseTitleAr, x.HolderNameAr, x.Status,
                x.IssuedAt, x.RevokedAt, x.IsActive))
            .ToListAsync(ct);
        return Result<CertificatePageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<IReadOnlyList<CertificateCardDto>>> MineAsync(
        long actorUserId, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!ScopeGuard.AllowsPersonalRead(effective, "certificates.view.own"))
            return Result<IReadOnlyList<CertificateCardDto>>.Failure("FORBIDDEN");
        string verifyBase = await CertificateVerificationUrl.BaseAsync(settings, ct);
        var rows = await db.Certificates.AsNoTracking()
            .Where(x => x.UserId == actorUserId && x.Status == CertificateStatus.Issued &&
                x.IsActive && !x.HiddenByCourseDisable && x.User != null &&
                x.User.Employee != null &&
                x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus)
            .OrderByDescending(x => x.IssuedAt)
            .Select(x => new CertificateCardDto(x.Id, x.ReferenceNo, x.CourseId,
                x.Course!.CourseNo, x.CourseTitleAr, x.CoursePeriodText, x.CourseHours,
                x.Course.InstructorUser != null &&
                x.Course.InstructorUser.AccountType == AccountType.Employee &&
                (x.Course.InstructorUser.Employee == null ||
                 x.Course.InstructorUser.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus)
                    ? "—" : x.InstructorNameAr,
                x.IssuedAt,
                CertificateVerificationUrl.Build(verifyBase, x.ReferenceNo)))
            .ToListAsync(ct);
        return Result<IReadOnlyList<CertificateCardDto>>.Success(rows);
    }

    public async Task<Result<FileDownloadDto>> GetPdfAsync(long actorUserId,
        long certificateId, CancellationToken ct)
    {
        var certificate = await db.Certificates
            .Include(x => x.PdfFile)
            .Where(x => x.Id == certificateId && x.Status == CertificateStatus.Issued)
            .SingleOrDefaultAsync(ct);
        if (certificate is null || !certificate.IsActive || certificate.PdfFile is null)
            return Result<FileDownloadDto>.Failure("CERT_NOT_FOUND");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (certificate.UserId != actorUserId ||
            !ScopeGuard.AllowsPersonalRead(effective, "certificates.download.own"))
            return Result<FileDownloadDto>.Failure("FORBIDDEN");
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorUserId &&
            x.Employee != null &&
            x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus, ct))
            return Result<FileDownloadDto>.Failure("FORBIDDEN");
        if (await db.Courses.AsNoTracking().AnyAsync(x => x.Id == certificate.CourseId &&
            x.InstructorUser != null && x.InstructorUser.AccountType == AccountType.Employee &&
            (x.InstructorUser.Employee == null ||
             x.InstructorUser.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus), ct))
            return Result<FileDownloadDto>.Failure("FORBIDDEN");
        string relativePath = certificate.PdfFile.RelativePath;
        if (certificate.PdfRenderVersion < CurrentPdfRenderVersion)
        {
            var regenerated = await RegeneratePdfAsync(certificate, actorUserId, ct);
            if (!regenerated.Ok)
                return Result<FileDownloadDto>.Failure(regenerated.Error!.Code);
            relativePath = regenerated.Value!;
        }
        Stream stream;
        try { stream = await storage.OpenReadAsync(relativePath, ct); }
        catch (FileNotFoundException)
        {
            return Result<FileDownloadDto>.Failure("FILE_CONTENT_MISSING");
        }
        try
        {
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
                "certificate.pdf_download", "تنزيل شهادة PDF", "Certificate PDF downloaded",
                "Certificate", certificateId, certificate.ReferenceNo,
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
        }
        catch { await stream.DisposeAsync(); throw; }
        return Result<FileDownloadDto>.Success(new(stream, $"{certificate.ReferenceNo}.pdf",
            "application/pdf"));
    }

    private async Task<Result<string>> RegeneratePdfAsync(
        Domain.Entities.Certificates.Certificate certificate, long actorUserId,
        CancellationToken ct)
    {
        var template = await db.CertificateTemplates.AsNoTracking()
            .Where(x => x.IsCurrent).OrderByDescending(x => x.VersionNo)
            .Select(x => new { x.Id, x.FileId, x.PageWidthPt, x.PageHeightPt, x.LayoutJson })
            .FirstOrDefaultAsync(ct);
        if (template is null) return Result<string>.Failure("CERT_TEMPLATE_MISSING");
        var templateFile = await db.StoredFiles.AsNoTracking()
            .Where(x => x.Id == template.FileId && x.IsActive)
            .Select(x => x.RelativePath).SingleOrDefaultAsync(ct);
        if (templateFile is null) return Result<string>.Failure("CERT_TEMPLATE_MISSING");
        byte[] templatePdf;
        try
        {
            await using var source = await storage.OpenReadAsync(templateFile, ct);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, ct);
            templatePdf = buffer.ToArray();
        }
        catch (FileNotFoundException)
        {
            return Result<string>.Failure("CERT_TEMPLATE_MISSING");
        }
        string verifyBase = await CertificateVerificationUrl.BaseAsync(settings, ct);
        string verifyUrl = CertificateVerificationUrl.Build(verifyBase, certificate.ReferenceNo);
        byte[] qrPng = qr.GeneratePng(verifyUrl, 600);
        var layer = new CertificateTextLayerInput(template.PageWidthPt, template.PageHeightPt,
            template.LayoutJson, certificate.HolderNameAr, certificate.HolderNameEn,
            certificate.CourseTitleAr, certificate.CourseTitleEn,
            certificate.CoursePeriodText,
            FormattableString.Invariant($"{certificate.CourseHours:0.##}"),
            certificate.InstructorNameAr, certificate.InstructorNameEn,
            TimeZoneInfo.ConvertTime(certificate.IssuedAt, clock.Amman).ToString("dd/MM/yyyy"),
            certificate.ReferenceNo, verifyUrl);
        byte[] pdf;
        try { pdf = renderer.Render(templatePdf, layer, qrPng); }
        catch { return Result<string>.Failure("CERT_RENDERER_FAILED"); }
        DateTimeOffset now = clock.UtcNow;
        string relative = await storage.WriteAsync(".pdf", pdf, now, ct);
        var replacement = new StoredFile
        {
            Kind = FileKind.CertificatePdf,
            OriginalName = $"{certificate.ReferenceNo}.pdf",
            StoredName = Path.GetFileName(relative),
            RelativePath = relative,
            MimeType = "application/pdf",
            SizeBytes = pdf.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(),
            UploadedBy = actorUserId,
            UploadedAt = now,
            IsActive = true
        };
        db.StoredFiles.Add(replacement);
        await db.SaveChangesAsync(ct);
        if (certificate.PdfFile is not null)
            certificate.PdfFile.IsActive = false;
        certificate.PdfFileId = replacement.Id;
        certificate.TemplateId = template.Id;
        certificate.PdfRenderVersion = CurrentPdfRenderVersion;
        certificate.UpdatedAt = now;
        certificate.UpdatedByUserId = actorUserId;
        await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
            "certificate.pdf_regenerated", "إعادة توليد ملف الشهادة",
            "Certificate PDF regenerated", "Certificate", certificate.Id,
            certificate.ReferenceNo, CourseId: certificate.CourseId,
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result<string>.Success(relative);
    }

    public async Task<Result<CertificateVerifyDto?>> VerifyAsync(string referenceNo,
        CancellationToken ct)
    {
        string reference = (referenceNo ?? string.Empty).Trim();
        if (reference.Length == 0 || reference.Length > 100)
            return Result<CertificateVerifyDto?>.Success(null);
        var certificate = await db.Certificates.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.ReferenceNo == reference && x.User != null &&
                x.User.Employee != null &&
                x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus)
            .Select(x => new
            {
                x.ReferenceNo,
                x.HolderNameAr,
                x.HolderNameEn,
                x.CourseTitleAr,
                x.CourseTitleEn,
                x.CoursePeriodText,
                x.CourseHours,
                InstructorNameAr = x.Course != null && x.Course.InstructorUser != null &&
                    x.Course.InstructorUser.AccountType == AccountType.Employee &&
                    (x.Course.InstructorUser.Employee == null ||
                     x.Course.InstructorUser.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus)
                    ? "—" : x.InstructorNameAr,
                x.IssuedAt,
                x.RevokedAt,
                x.Status,
                x.IsActive,
                x.HiddenByCourseDisable
            })
            .SingleOrDefaultAsync(ct);
        if (certificate is null || !certificate.IsActive || certificate.HiddenByCourseDisable)
            return Result<CertificateVerifyDto?>.Success(null);
        return Result<CertificateVerifyDto?>.Success(new CertificateVerifyDto(
            certificate.ReferenceNo, certificate.HolderNameAr, certificate.HolderNameEn,
            certificate.CourseTitleAr, certificate.CourseTitleEn, certificate.CoursePeriodText,
            certificate.CourseHours, certificate.InstructorNameAr, certificate.IssuedAt,
            certificate.RevokedAt, certificate.Status == CertificateStatus.Revoked));
    }

    private static string Base64UrlToken(int bytes)
    {
        byte[] raw = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(raw).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
    }

    private static string AppendAdministrativeCompletionNote(string? existing,
        long actorUserId, DateTimeOffset completedAt)
    {
        string note = $"Administrative completion override by user {actorUserId} at {completedAt:O}";
        if (string.IsNullOrWhiteSpace(existing)) return note;
        return existing.Contains("Administrative completion override", StringComparison.Ordinal)
            ? existing : $"{existing.Trim()}\n{note}";
    }
}
