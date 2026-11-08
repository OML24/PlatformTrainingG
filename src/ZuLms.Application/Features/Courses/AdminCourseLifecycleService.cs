using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class AdminCourseLifecycleService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilitySnapshotService eligibility,
    IAuditLogger audit) : IAdminCourseLifecycleService
{
    public async Task<Result<CourseDeactivationPreviewDto>> PreviewDeactivationAsync(
        long actorUserId, long courseId, CancellationToken ct)
    {
        if (!await CanAsync(actorUserId, "course.deactivate", ct))
            return Result<CourseDeactivationPreviewDto>.Failure("FORBIDDEN");
        var course = await db.Courses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => new { x.Id, x.Version, x.IsActive })
            .SingleOrDefaultAsync(ct);
        if (course is null)
            return Result<CourseDeactivationPreviewDto>.Failure("COURSE_NOT_FOUND");
        if (!course.IsActive)
            return Result<CourseDeactivationPreviewDto>.Failure("COURSE_ALREADY_DISABLED");
        int issued = await db.Certificates.IgnoreQueryFilters().CountAsync(x =>
            x.CourseId == courseId && x.Status == CertificateStatus.Issued, ct);
        return Result<CourseDeactivationPreviewDto>.Success(new(courseId,
            course.Version, issued));
    }

    public async Task<Result> DeactivateAsync(long actorUserId, long courseId,
        uint expectedVersion, string reason,
        CertificateDeactivationAction? certificateAction, CancellationToken ct)
    {
        if (!await CanAsync(actorUserId, "course.deactivate", ct))
            return Result.Failure("FORBIDDEN");
        if (!ValidReason(reason)) return Result.Failure("REASON_REQUIRED");
        if (certificateAction is { } action && !Enum.IsDefined(action))
            return Result.Failure("CERTIFICATE_ACTION_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters().SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (!course.IsActive) return Result.Failure("COURSE_ALREADY_DISABLED");
            var certificates = await db.Certificates.IgnoreQueryFilters()
                .Where(x => x.CourseId == courseId &&
                    x.Status == CertificateStatus.Issued).ToListAsync(ct);
            if (certificates.Count > 0 && certificateAction is null)
                return Result.Failure("CERTIFICATE_ACTION_REQUIRED");
            DateTimeOffset now = clock.UtcNow;
            course.IsActive = false;
            course.DeactivatedAt = now;
            course.DeactivatedByUserId = actorUserId;
            course.DeactivationReason = reason.Trim();
            course.DeactivationCertificatesAction = certificateAction?.ToString();
            course.UpdatedAt = now; course.UpdatedByUserId = actorUserId;
            if (certificateAction == CertificateDeactivationAction.Hide)
                foreach (var certificate in certificates)
                {
                    certificate.IsActive = false;
                    certificate.HiddenByCourseDisable = true;
                    certificate.DeactivatedAt = now;
                    certificate.DeactivatedByUserId = actorUserId;
                    certificate.DeactivationReason = "Course disabled";
                }
            await NotifyAsync(course, NotificationType.CourseDisabled,
                "تعطيل دورة", "Course disabled", now, ct);
            await LogAsync("course.deactivate", course, actorUserId,
                new { reason, certificateAction, issued = certificates.Count }, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> RestoreAsync(long actorUserId, long courseId,
        uint expectedVersion, CancellationToken ct)
    {
        if (!await CanAsync(actorUserId, "course.restore", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters().SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (course.IsActive) return Result.Failure("COURSE_ALREADY_ACTIVE");
            var hidden = await db.Certificates.IgnoreQueryFilters()
                .Where(x => x.CourseId == courseId && x.HiddenByCourseDisable)
                .ToListAsync(ct);
            DateTimeOffset now = clock.UtcNow;
            int restored = 0, revoked = 0;
            foreach (var certificate in hidden)
            {
                certificate.HiddenByCourseDisable = false;
                certificate.IsActive = true;
                certificate.DeactivatedAt = null;
                certificate.DeactivatedByUserId = null;
                certificate.DeactivationReason = null;
                if (certificate.Status == CertificateStatus.Revoked)
                    continue;
                var result = await eligibility.EvaluateAsync(courseId,
                    certificate.ParticipantId, true, ct);
                if (!result.Ok) return Result.Failure(result.Error!.Code);
                if (result.Value!.Eligible) restored++;
                else
                {
                    certificate.Status = CertificateStatus.Revoked;
                    certificate.RevokedAt = now;
                    certificate.RevokedBy = actorUserId;
                    certificate.RevokedReason =
                        CertificateRevokeReason.CourseDisabledRestoreIneligible;
                    certificate.RevokedNote = "Eligibility lost while course was disabled";
                    revoked++;
                    Notify([certificate.UserId], NotificationType.CertificateRevoked,
                        "إلغاء شهادة", "Certificate revoked", course, now);
                }
            }
            course.IsActive = true;
            course.DeactivationCertificatesAction = null;
            course.DeactivatedAt = null;
            course.DeactivatedByUserId = null;
            course.DeactivationReason = null;
            course.UpdatedAt = now; course.UpdatedByUserId = actorUserId;
            await NotifyAsync(course, NotificationType.CourseRestored,
                "استعادة دورة", "Course restored", now, ct);
            await LogAsync("course.restore", course, actorUserId,
                new { restored, revoked }, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> ArchiveAsync(long actorUserId, long courseId,
        uint expectedVersion, bool archive, CancellationToken ct)
    {
        if (!await CanAsync(actorUserId,
            archive ? "course.archive" : "course.unarchive", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters().SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (course.IsArchived == archive)
                return Result.Failure(archive ? "COURSE_ALREADY_ARCHIVED" :
                    "COURSE_NOT_ARCHIVED");
            DateTimeOffset now = clock.UtcNow;
            course.IsArchived = archive;
            course.ArchivedAt = archive ? now : null;
            course.ArchivedByUserId = archive ? actorUserId : null;
            course.UpdatedAt = now; course.UpdatedByUserId = actorUserId;
            await NotifyAsync(course, archive ? NotificationType.CourseArchived :
                NotificationType.CourseUnarchived,
                archive ? "أرشفة دورة" : "إلغاء أرشفة دورة",
                archive ? "Course archived" : "Course unarchived", now, ct);
            await LogAsync(archive ? "course.archive" : "course.unarchive",
                course, actorUserId, new { archive }, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> ReopenAsync(long actorUserId, long courseId,
        uint expectedVersion, bool confirmed, CancellationToken ct)
    {
        if (!await CanAsync(actorUserId, "course.reopen", ct))
            return Result.Failure("FORBIDDEN");
        if (!confirmed) return Result.Failure("CONFIRM_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters().SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (course.Status != CourseStatus.Finished)
                return Result.Failure("REOPEN_NOT_ALLOWED");
            DateTimeOffset now = clock.UtcNow;
            var certificates = await db.Certificates.IgnoreQueryFilters()
                .Where(x => x.CourseId == courseId &&
                    x.Status == CertificateStatus.Issued).ToListAsync(ct);
            foreach (var certificate in certificates)
            {
                certificate.Status = CertificateStatus.Revoked;
                certificate.RevokedAt = now;
                certificate.RevokedBy = actorUserId;
                certificate.RevokedReason = CertificateRevokeReason.CourseReopened;
                certificate.RevokedNote = "Course reopened";
                if (certificate.IsActive)
                    Notify([certificate.UserId], NotificationType.CertificateRevoked,
                        "إلغاء شهادة", "Certificate revoked", course, now);
            }
            course.Status = CourseStatus.Approved;
            course.StatusChangedAt = now;
            course.FinalLecturesCount = null;
            course.FinishedAt = null; course.FinishedBy = null;
            course.UpdatedAt = now; course.UpdatedByUserId = actorUserId;
            db.CourseStatusHistorys.Add(new CourseStatusHistory
            {
                CourseId = courseId,
                FromStatus = CourseStatus.Finished,
                ToStatus = CourseStatus.Approved,
                ChangedBy = actorUserId,
                ChangedAt = now,
                Reason = "Course reopened by administrator"
            });
            await LogAsync("course.reopen", course, actorUserId,
                new { revokedCertificates = certificates.Count }, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<bool> CanAsync(long actorUserId, string key,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, key, ct)) return false;
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        return effective.HasMode(RoleCode.Admin);
    }

    private async Task NotifyAsync(Course course, NotificationType type,
        string titleAr, string titleEn, DateTimeOffset now, CancellationToken ct)
    {
        var recipients = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == course.Id && x.IsActive)
            .Select(x => x.UserId).ToListAsync(ct);
        if (course.InstructorUserId is long instructorId) recipients.Add(instructorId);
        var qualityIds = await db.UserRoles.AsNoTracking().Where(x => x.IsActive &&
            x.Role!.Code == RoleCode.Quality && x.User!.IsActive)
            .Select(x => x.UserId).Distinct().ToListAsync(ct);
        recipients.AddRange(qualityIds);
        Notify(recipients.Distinct(), type, titleAr, titleEn, course, now);
    }

    private void Notify(IEnumerable<long> recipients, NotificationType type,
        string titleAr, string titleEn, Course course, DateTimeOffset now)
    {
        foreach (long userId in recipients)
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = type,
                TitleAr = titleAr,
                TitleEn = titleEn,
                BodyAr = course.TitleAr,
                BodyEn = course.TitleEn,
                LinkUrl = $"/courses/{course.Id}",
                RelatedEntityType = "Course",
                RelatedEntityId = course.Id,
                CreatedAt = now,
                IsActive = true
            });
    }

    private Task LogAsync(string action, Course course, long actorUserId,
        object after, CancellationToken ct) => audit.LogAsync(new AuditEntry(
            AuditCategory.Course, action, "إدارة الدورة", "Course administration",
            "Course", course.Id, course.TitleAr,
            AfterJson: JsonSerializer.Serialize(after),
            ActorUserId: actorUserId), ct);

    private static bool ValidReason(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 1000;
}
