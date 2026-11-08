using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface ICourseForceStatusService
{
    Task<Result> ChangeAsync(long actorUserId, long courseId, uint expectedVersion,
        CourseStatus target, string reason, bool confirmed, CancellationToken ct);
}

public sealed class CourseForceStatusService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilityRecalculationService recalculation,
    IAuditLogger audit) : ICourseForceStatusService
{
    public async Task<Result> ChangeAsync(long actorUserId, long courseId,
        uint expectedVersion, CourseStatus target, string reason, bool confirmed,
        CancellationToken ct)
    {
        var access = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!access.HasMode(RoleCode.Admin) ||
            !access.Keys.Contains("course.force_status"))
            return Result.Failure("FORBIDDEN");
        if (!confirmed) return Result.Failure("CONFIRM_REQUIRED");
        if (!Enum.IsDefined(target)) return Result.Failure("STATUS_INVALID");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return Result.Failure("REASON_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            CourseStatus previous = course.Status;
            if (previous == target) return Result.Failure("STATUS_UNCHANGED");
            DateTimeOffset now = clock.UtcNow;
            string note = reason.Trim();
            int revoked = 0;
            if (target != CourseStatus.Finished)
            {
                var certificates = await db.Certificates.IgnoreQueryFilters()
                    .Where(x => x.CourseId == courseId &&
                        x.Status == CertificateStatus.Issued).ToListAsync(ct);
                foreach (var certificate in certificates)
                {
                    certificate.Status = CertificateStatus.Revoked;
                    certificate.RevokedAt = now;
                    certificate.RevokedBy = actorUserId;
                    certificate.RevokedReason = target == CourseStatus.Cancelled
                        ? CertificateRevokeReason.CourseCancelled
                        : previous == CourseStatus.Finished && target == CourseStatus.Approved
                            ? CertificateRevokeReason.CourseReopened
                            : CertificateRevokeReason.ManualByAdmin;
                    certificate.RevokedNote = note;
                    revoked++;
                    db.Notifications.Add(new Notification
                    {
                        UserId = certificate.UserId,
                        Type = NotificationType.CertificateRevoked,
                        TitleAr = "إلغاء شهادة بعد تغيير حالة الدورة",
                        TitleEn = "Certificate revoked after course status change",
                        BodyAr = course.TitleAr,
                        BodyEn = course.TitleEn,
                        LinkUrl = $"/courses/{courseId}",
                        RelatedEntityType = "Certificate",
                        RelatedEntityId = certificate.Id,
                        CreatedAt = now,
                        IsActive = true
                    });
                }
            }

            course.Status = target;
            course.StatusChangedAt = now;
            course.UpdatedAt = now;
            course.UpdatedByUserId = actorUserId;
            if (target == CourseStatus.Finished)
            {
                course.FinishedAt = now;
                course.FinishedBy = actorUserId;
                course.FinalLecturesCount = await db.Lectures.CountAsync(x =>
                    x.CourseId == courseId && x.IsActive, ct);
            }
            else
            {
                course.FinishedAt = null;
                course.FinishedBy = null;
                course.FinalLecturesCount = null;
            }
            if (target == CourseStatus.Cancelled)
            {
                course.CancelledAt = now;
                course.CancelledBy = actorUserId;
                course.CancellationReason = note;
                course.IsArchived = true;
                course.ArchivedAt = now;
                course.ArchivedByUserId = actorUserId;
            }
            else if (previous == CourseStatus.Cancelled)
            {
                course.CancelledAt = null;
                course.CancelledBy = null;
                course.CancellationReason = null;
            }
            if (target == CourseStatus.Approved && course.ApprovedAt is null)
            {
                course.ApprovedAt = now;
                course.ApprovedBy = actorUserId;
            }
            if (target == CourseStatus.Returned) course.LastReturnNotes = note;
            db.CourseStatusHistorys.Add(new CourseStatusHistory
            {
                CourseId = courseId,
                FromStatus = previous,
                ToStatus = target,
                ChangedBy = actorUserId,
                ChangedAt = now,
                Reason = $"[Admin force] {note}"
            });
            if (target == CourseStatus.Finished)
            {
                await db.SaveChangesAsync(ct);
                var eligibility = await recalculation.RecalculateCourseAsync(
                    courseId, null, actorUserId, ct);
                if (!eligibility.Ok)
                    throw new InvalidOperationException($"Forced finish recalculation failed: {eligibility.Error!.Code}");
            }
            var recipients = await db.CourseParticipants.AsNoTracking()
                .Where(x => x.CourseId == courseId && x.IsActive)
                .Select(x => x.UserId).ToListAsync(ct);
            if (course.InstructorUserId is long instructor) recipients.Add(instructor);
            if (course.ProposedByUserId is long proposer) recipients.Add(proposer);
            NotificationType type = target switch
            {
                CourseStatus.Finished => NotificationType.CourseFinished,
                CourseStatus.Cancelled => NotificationType.CourseCancelled,
                CourseStatus.Approved => NotificationType.CourseApproved,
                _ => NotificationType.ProposalDecision
            };
            foreach (long recipient in recipients.Distinct())
                db.Notifications.Add(new Notification
                {
                    UserId = recipient,
                    Type = type,
                    TitleAr = "تغيير حالة الدورة",
                    TitleEn = "Course status changed",
                    BodyAr = $"{course.TitleAr} — {target}",
                    BodyEn = $"{course.TitleEn ?? course.TitleAr} — {target}",
                    LinkUrl = $"/courses/{courseId}",
                    RelatedEntityType = "Course",
                    RelatedEntityId = courseId,
                    CreatedAt = now,
                    IsActive = true
                });
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "course.force_status", "تغيير حالة الدورة قسرياً",
                "Course status changed by administrator", "Course", courseId,
                course.TitleAr, courseId,
                BeforeJson: JsonSerializer.Serialize(new { status = previous }),
                AfterJson: JsonSerializer.Serialize(new
                {
                    status = target,
                    reason = note,
                    revokedCertificates = revoked
                }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}
