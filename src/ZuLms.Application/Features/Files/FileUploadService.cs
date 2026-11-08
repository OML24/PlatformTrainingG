using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Entities.Files;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Files;

public sealed class FileUploadService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings,
    IFileStorage storage, IAuditLogger audit) : IFileUploadService
{
    public async Task<Result<FileUploadDto>> UploadAsync(long actorUserId,
        FileKind kind, string originalName, Stream content, long declaredLength,
        CancellationToken ct)
    {
        string[] permissionKeys = kind switch
        {
            FileKind.ProposalCouncilLetter or FileKind.InstructorCv =>
                ["course.propose", "course.create_direct", "course.edit"],
            FileKind.CancellationLetter =>
                ["cancellation.request", "course.cancel_direct"],
            FileKind.TrainingMaterial => ["lecture.manage"],
            FileKind.ImpactFile => ["impact.submit"],
            FileKind.CertificateTemplate => ["cert_template.upload"],
            FileKind.NeedLetter => ["need.submit"],
            FileKind.ProfessionalCertificate => ["procert.submit"],
            FileKind.QualityMinutes or FileKind.Recommendation => ["vault.upload"],
            FileKind.ProfilePhoto => ["profile.edit.own"],
            _ => []
        };
        if (permissionKeys.Length == 0) return Result<FileUploadDto>.Failure("FILE_KIND_UNSUPPORTED");
        bool allowed = false;
        foreach (string permission in permissionKeys)
            if (await permissions.HasAsync(actorUserId, permission, ct)) { allowed = true; break; }
        if (!allowed) return Result<FileUploadDto>.Failure("FORBIDDEN");
        string configKey = kind switch
        {
            FileKind.InstructorCv => "cv",
            FileKind.TrainingMaterial => "materials",
            FileKind.ImpactFile => "impact",
            FileKind.CertificateTemplate => "cert_template",
            FileKind.ProfessionalCertificate => "pro_cert",
            FileKind.ProfilePhoto => "photo",
            _ => "letters"
        };
        string extension = Path.GetExtension(originalName).TrimStart('.').ToLowerInvariant();
        string extensions = await settings.GetAsync<string>($"files.ext.{configKey}", ct);
        string[] allowedExtensions = extensions.Split(',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (extension.Length == 0 || !allowedExtensions.Contains(extension,
            StringComparer.OrdinalIgnoreCase))
            return Result<FileUploadDto>.Failure("FILE_EXTENSION_INVALID");
        string sizeKey = kind switch
        {
            FileKind.TrainingMaterial => "files.max_mb.materials",
            FileKind.ImpactFile => "files.max_mb.impact",
            FileKind.CertificateTemplate => "files.max_mb.cert_template",
            FileKind.ProfessionalCertificate => "files.max_mb.pro_cert",
            FileKind.ProfilePhoto => "files.max_mb.photo",
            _ => "files.max_mb.letters"
        };
        int maxMb = await settings.GetAsync<int>(sizeKey, ct);
        long maxBytes = checked((long)maxMb * 1024 * 1024);
        if (declaredLength <= 0 || declaredLength > maxBytes)
            return Result<FileUploadDto>.Failure("FILE_TOO_LARGE");
        await using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        while (true)
        {
            int read = await content.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > maxBytes)
                return Result<FileUploadDto>.Failure("FILE_TOO_LARGE");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length != declaredLength)
            return Result<FileUploadDto>.Failure("FILE_LENGTH_MISMATCH");
        if (buffer.Length == 0 || !FileRules.IsAllowed(originalName,
            buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)), allowedExtensions))
            return Result<FileUploadDto>.Failure("FILE_TYPE_INVALID");
        byte[] bytes = buffer.ToArray();
        string cleanName = new string(Path.GetFileName(originalName.Replace('\\', '/'))
            .Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleanName.Length == 0) cleanName = $"upload.{extension}";
        if (cleanName.Length > 300) cleanName = cleanName[..300];
        string mime = extension switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ppt" => "application/vnd.ms-powerpoint",
            "zip" => "application/zip",
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            _ => throw new InvalidOperationException("Unsupported validated extension.")
        };
        DateTimeOffset now = clock.UtcNow;
        string relative = await storage.WriteAsync($".{extension}", bytes, now, ct);
        try
        {
            return await db.InTransactionAsync(async () =>
            {
                var file = new StoredFile
                {
                    Kind = kind,
                    OriginalName = cleanName,
                    StoredName = Path.GetFileName(relative),
                    RelativePath = relative,
                    MimeType = mime,
                    SizeBytes = bytes.LongLength,
                    Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    UploadedBy = actorUserId,
                    UploadedAt = now,
                    IsActive = true
                };
                db.StoredFiles.Add(file);
                await db.SaveChangesAsync(ct);
                if (kind is FileKind.ProposalCouncilLetter or FileKind.InstructorCv or
                    FileKind.CancellationLetter or FileKind.NeedLetter or
                    FileKind.ProfessionalCertificate)
                    db.VaultDocuments.Add(new VaultDocument
                    {
                        FileId = file.Id,
                        DocType = kind,
                        TitleAr = cleanName,
                        SourceEntityType = "StoredFile",
                        SourceEntityId = file.Id,
                        UploadedBy = actorUserId,
                        UploadedAt = now,
                        CreatedAt = now,
                        CreatedByUserId = actorUserId,
                        IsActive = true
                    });
                await audit.LogAsync(new AuditEntry(AuditCategory.Document, "file.upload",
                    "رفع ملف", "File uploaded", "StoredFile", file.Id,
                    cleanName, ActorUserId: actorUserId), ct);
                await db.SaveChangesAsync(ct);
                return Result<FileUploadDto>.Success(new(file.Id, kind,
                    file.OriginalName, file.SizeBytes));
            }, ct);
        }
        catch
        {
            await storage.DeleteUncommittedAsync(relative);
            throw;
        }
    }

    public async Task<Result<FileDownloadDto>> DownloadAsync(long actorUserId,
        long fileId, CancellationToken ct)
    {
        var file = await db.StoredFiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == fileId && x.IsActive, ct);
        if (file is null) return Result<FileDownloadDto>.Failure("FILE_NOT_FOUND");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!await CanDownloadAsync(actorUserId, fileId, file.Kind, effective, ct))
            return Result<FileDownloadDto>.Failure("FORBIDDEN");
        Stream stream;
        try { stream = await storage.OpenReadAsync(file.RelativePath, ct); }
        catch (FileNotFoundException) { return Result<FileDownloadDto>.Failure("FILE_CONTENT_MISSING"); }
        try
        {
            await audit.LogAsync(new AuditEntry(AuditCategory.Document, "file.download",
                "تنزيل ملف", "File downloaded", "StoredFile", file.Id,
                file.OriginalName, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
        return Result<FileDownloadDto>.Success(new(stream, file.OriginalName,
            file.MimeType));
    }

    private async Task<bool> CanDownloadAsync(long actorUserId, long fileId,
        FileKind kind, EffectivePermissionSet effective, CancellationToken ct)
    {
        bool vaultViewer = FileDownloadAccessPolicy.AllowsVaultDownload(effective, kind) &&
            await db.VaultDocuments.AsNoTracking().AnyAsync(x =>
                x.FileId == fileId && x.IsActive, ct);

        switch (kind)
        {
            case FileKind.TrainingMaterial:
                return vaultViewer ||
                    (effective.HasMode(RoleCode.Admin) && effective.Keys.Contains("lecture.manage") &&
                     await HasActiveMaterialLinkAsync(fileId, ct)) ||
                    ((effective.HasMode(RoleCode.Instructor) ||
                      effective.HasMode(RoleCode.ExternalInstructor)) &&
                     effective.Keys.Contains("lecture.manage") &&
                     await db.CourseFiles.AsNoTracking().AnyAsync(x => x.FileId == fileId && x.IsActive &&
                         x.LectureId != null && x.Lecture!.IsActive && x.Course!.IsActive &&
                         x.Course.InstructorUserId == actorUserId, ct)) ||
                    (effective.HasMode(RoleCode.Quality) && effective.Keys.Contains("lecture.manage") &&
                     await db.CourseFiles.AsNoTracking().AnyAsync(x => x.FileId == fileId && x.IsActive &&
                         x.LectureId != null && x.Lecture!.IsActive && x.Course!.IsActive &&
                         x.Course.ManagedByQuality, ct)) ||
                    (ScopeGuard.AllowsPersonalRead(effective, "page.member.enrolled") &&
                     await db.CourseFiles.AsNoTracking().AnyAsync(x => x.FileId == fileId &&
                         x.IsActive && x.LectureId != null && x.Lecture!.IsActive &&
                         x.Course!.IsActive && db.CourseParticipants.Any(p =>
                             p.CourseId == x.CourseId && p.UserId == actorUserId && p.IsActive), ct));

            case FileKind.ProfessionalCertificate:
                return FileDownloadAccessPolicy.AllowsPersonalDownload(kind, effective) &&
                    await db.ProfessionalCertifications.AsNoTracking().AnyAsync(x =>
                        x.FileId == fileId && x.IsActive && x.UserId == actorUserId &&
                        x.User!.Employee != null &&
                        x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus, ct);

            case FileKind.ImpactFile:
                if (FileDownloadAccessPolicy.AllowsPersonalDownload(kind, effective) &&
                    await db.ImpactSubmissions.AsNoTracking().AnyAsync(x => x.FileId == fileId &&
                        x.IsActive && x.Participant!.IsActive && x.Participant.UserId == actorUserId &&
                        x.Participant.User!.Employee != null &&
                        x.Participant.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus, ct))
                    return true;
                return await CanReviewImpactFileAsync(actorUserId, fileId, effective, ct);

            case FileKind.CertificatePdf:
                if (FileDownloadAccessPolicy.AllowsPersonalDownload(kind, effective) &&
                    await db.Certificates.AsNoTracking().AnyAsync(x => x.PdfFileId == fileId &&
                        x.IsActive && x.UserId == actorUserId && x.User != null &&
                        x.User.Employee != null &&
                        x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus &&
                        (x.Course!.InstructorUser == null ||
                         x.Course.InstructorUser.AccountType != AccountType.Employee ||
                         x.Course.InstructorUser.Employee != null &&
                         x.Course.InstructorUser.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus), ct))
                    return true;
                return await CanReviewCertificateFileAsync(actorUserId, fileId, effective, ct);

            case FileKind.ProfilePhoto:
                return effective.ActiveMode is { } profileMode && effective.Roles.Contains(profileMode) &&
                    effective.Keys.Contains("profile.edit.own") &&
                    await db.MemberProfiles.AsNoTracking().AnyAsync(x =>
                        x.PhotoFileId == fileId && x.UserId == actorUserId, ct);

            case FileKind.NeedLetter:
                if (await db.TrainingNeeds.AsNoTracking().AnyAsync(x => x.LetterFileId == fileId &&
                    (effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality)) &&
                    effective.Keys.Contains("need.review"), ct)) return true;
                return effective.HasMode(RoleCode.Dean) && effective.Keys.Contains("need.submit") &&
                    await db.TrainingNeeds.AsNoTracking().AnyAsync(x => x.LetterFileId == fileId &&
                        effective.DeanScopeCenterIds.Contains(x.CenterId), ct);

            case FileKind.ProposalCouncilLetter:
            case FileKind.InstructorCv:
            case FileKind.CancellationLetter:
                return vaultViewer || await CanReadCourseDocumentAsync(actorUserId,
                    fileId, kind, effective, ct);

            case FileKind.CertificateTemplate:
                return effective.HasMode(RoleCode.Admin) &&
                    effective.Keys.Contains("cert_template.upload") &&
                    await db.CertificateTemplates.AsNoTracking().AnyAsync(x => x.FileId == fileId, ct);

            case FileKind.QualityMinutes:
            case FileKind.Recommendation:
                return vaultViewer;

            case FileKind.Other:
            default:
                return false;
        }
    }

    private Task<bool> HasActiveMaterialLinkAsync(long fileId, CancellationToken ct) =>
        db.CourseFiles.AsNoTracking().AnyAsync(x => x.FileId == fileId && x.IsActive &&
            x.LectureId != null && x.Lecture!.IsActive && x.Course!.IsActive, ct);

    private async Task<bool> CanReadCourseDocumentAsync(long actorUserId, long fileId,
        FileKind kind, EffectivePermissionSet effective, CancellationToken ct)
    {
        var courses = db.CourseFiles.AsNoTracking().Where(x => x.FileId == fileId && x.IsActive)
            .Select(x => x.Course!);
        bool linked = await courses.AnyAsync(ct);
        if (linked && effective.HasMode(RoleCode.Admin) && effective.Keys.Contains("course.edit"))
            return true;
        if (linked && effective.HasMode(RoleCode.Quality) &&
            effective.Keys.Contains("page.quality.courses") &&
            await courses.AnyAsync(x => x.ManagedByQuality, ct)) return true;
        if (linked && effective.HasMode(RoleCode.Dean) && effective.Keys.Contains("course.propose") &&
            await courses.AnyAsync(x =>
                x.ProposingCenterId != null && effective.DeanScopeCenterIds.Contains(x.ProposingCenterId.Value) ||
                x.TargetCenterId != null && effective.DeanScopeCenterIds.Contains(x.TargetCenterId.Value), ct))
            return true;
        if (kind == FileKind.CancellationLetter && effective.HasMode(RoleCode.Quality) &&
            effective.Keys.Contains("cancellation.decide") &&
            await db.CourseCancellationRequests.AsNoTracking().AnyAsync(x =>
                x.LetterFileId == fileId && x.Course!.ManagedByQuality, ct)) return true;
        return false;
    }

    private async Task<bool> CanReviewImpactFileAsync(long actorUserId, long fileId,
        EffectivePermissionSet effective, CancellationToken ct)
    {
        if (!effective.Keys.Contains("impact.review")) return false;
        var impactCourse = await db.ImpactSubmissions.AsNoTracking()
            .Where(x => x.FileId == fileId && x.Participant!.Course!.IsActive)
            .Select(x => new
            {
                VisibleOwner = x.Participant!.User!.Employee != null &&
                    x.Participant.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus,
                x.Participant!.Course!.Origin,
                x.Participant.Course.AudienceType,
                x.Participant.Course.ProposingCenterId,
                x.Participant.Course.TargetCenterId,
                x.Participant.Course.ManagedByQuality
            }).SingleOrDefaultAsync(ct);
        if (impactCourse is null) return false;
        if (!effective.HasMode(RoleCode.Admin) && !impactCourse.VisibleOwner) return false;
        bool deanDestination = ImpactReviewPolicy.IsDeanDestination(
            impactCourse.Origin, impactCourse.AudienceType,
            impactCourse.ProposingCenterId, impactCourse.TargetCenterId,
            impactCourse.ManagedByQuality);
        return effective.HasMode(RoleCode.Admin) ||
            !deanDestination && effective.HasMode(RoleCode.Quality) ||
            deanDestination && effective.HasMode(RoleCode.Dean) &&
            impactCourse.ProposingCenterId is long centerId &&
            effective.DeanScopeCenterIds.Contains(centerId);
    }

    private async Task<bool> CanReviewCertificateFileAsync(long actorUserId,
        long fileId, EffectivePermissionSet effective, CancellationToken ct)
    {
        bool all = (effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality)) &&
            effective.Keys.Contains("certificates.view.all");
        bool admin = effective.HasMode(RoleCode.Admin);
        if (all && await db.Certificates.AsNoTracking().AnyAsync(x => x.PdfFileId == fileId &&
            (admin || x.User != null && x.User.Employee != null &&
             x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus &&
             (x.Course!.InstructorUser == null ||
              x.Course.InstructorUser.AccountType != AccountType.Employee ||
              x.Course.InstructorUser.Employee != null &&
              x.Course.InstructorUser.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus)), ct))
            return true;
        if (!effective.HasMode(RoleCode.Dean) ||
            !effective.Keys.Contains("certificates.view.faculty")) return false;
        return await db.Certificates.AsNoTracking().AnyAsync(x => x.PdfFileId == fileId &&
            x.User != null && x.User.Employee != null &&
            x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus &&
            (x.Course!.InstructorUser == null ||
             x.Course.InstructorUser.AccountType != AccountType.Employee ||
             x.Course.InstructorUser.Employee != null &&
             x.Course.InstructorUser.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus) &&
            (x.Course!.ProposingCenterId != null &&
             effective.DeanScopeCenterIds.Contains(x.Course.ProposingCenterId.Value) ||
             x.Course.TargetCenterId != null &&
             effective.DeanScopeCenterIds.Contains(x.Course.TargetCenterId.Value)), ct);
    }
}
