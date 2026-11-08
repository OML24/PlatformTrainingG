using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class CourseCancellationService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit)
    : ICourseCancellationService
{
    public async Task<Result<long>> RequestAsync(long actorUserId, long courseId,
        string reason, long? letterFileId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "cancellation.request", ct))
            return Result<long>.Failure("FORBIDDEN");
        if (!ValidReason(reason)) return Result<long>.Failure("CANCELLATION_REASON_REQUIRED");
        bool required = await settings.GetAsync<bool>("courses.cancel_requires_letter", ct);
        if (required && letterFileId is null)
            return Result<long>.Failure("CANCELLATION_LETTER_REQUIRED");
        if (letterFileId is long fileId &&
            !await ValidLetterAsync(actorUserId, fileId, ct))
            return Result<long>.Failure("CANCELLATION_LETTER_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result<long>.Failure("COURSE_NOT_FOUND");
            var actorPermissions = await permissions.GetEffectiveAsync(actorUserId, ct);
            bool dean = actorPermissions.HasMode(RoleCode.Dean) &&
                (course.ProposingCenterId is long proposing && actorPermissions.DeanScopeCenterIds.Contains(proposing) ||
                 course.TargetCenterId is long target && actorPermissions.DeanScopeCenterIds.Contains(target));
            if (!dean) return Result<long>.Failure("FORBIDDEN");
            if (course.Status != CourseStatus.Approved)
                return Result<long>.Failure("CANCELLATION_NOT_ALLOWED");
            if (await db.CourseCancellationRequests.AnyAsync(x => x.CourseId == courseId &&
                x.Status == RequestStatus.Pending, ct))
                return Result<long>.Failure("CANCELLATION_ALREADY_PENDING");
            DateTimeOffset now = clock.UtcNow;
            var request = new CourseCancellationRequest
            {
                CourseId = courseId,
                RequestedBy = actorUserId,
                Reason = reason.Trim(),
                LetterFileId = letterFileId,
                Status = RequestStatus.Pending,
                CreatedAt = now,
                CreatedByUserId = actorUserId
            };
            db.CourseCancellationRequests.Add(request);
            await db.SaveChangesAsync(ct);
            if (letterFileId is long id)
                await LinkLetterAsync(course, id, actorUserId, now, ct);
            var qualityIds = await db.UserRoles.AsNoTracking().Where(x => x.IsActive &&
                x.Role!.Code == RoleCode.Quality && x.User!.IsActive)
                .Select(x => x.UserId).Distinct().ToListAsync(ct);
            Notify(qualityIds, NotificationType.CancellationRequested,
                "طلب إلغاء دورة", "Course cancellation requested", course, now);
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "cancellation.request", "طلب إلغاء دورة", "Course cancellation requested",
                "Course", course.Id, course.TitleAr,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(new
                { reason = reason.Trim() }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(request.Id);
        }, ct);
    }

    public async Task<Result<IReadOnlyList<CancellationRequestDto>>> PendingAsync(
        long actorUserId, CancellationToken ct)
    {
        if (!await CanDecideAsync(actorUserId, ct))
            return Result<IReadOnlyList<CancellationRequestDto>>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool admin = effective.HasMode(RoleCode.Admin);
        var query = db.CourseCancellationRequests.AsNoTracking().Where(x => x.Status == RequestStatus.Pending);
        var rows = await query
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new CancellationRequestDto(x.Id, x.CourseId,
                x.Course!.CourseNo, x.Course.TitleAr, x.Reason,
                x.LetterFileId, x.CreatedAt,
                admin || x.RequestedByUser!.AccountType != AccountType.Employee ||
                x.RequestedByUser.Employee != null &&
                x.RequestedByUser.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus
                    ? x.RequestedByUser!.FullNameAr : "—"))
            .ToListAsync(ct);
        return Result<IReadOnlyList<CancellationRequestDto>>.Success(rows);
    }

    public async Task<Result> DecideAsync(long actorUserId, long requestId,
        bool approve, string? notes, CancellationToken ct)
    {
        if (!await CanDecideAsync(actorUserId, ct)) return Result.Failure("FORBIDDEN");
        if (!approve && !ValidReason(notes))
            return Result.Failure("DECISION_NOTES_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var request = await db.CourseCancellationRequests.SingleOrDefaultAsync(x =>
                x.Id == requestId, ct);
            if (request is null) return Result.Failure("CANCELLATION_REQUEST_NOT_FOUND");
            if (request.Status != RequestStatus.Pending)
                return Result.Failure("CANCELLATION_ALREADY_DECIDED");
            var course = await db.Courses.SingleAsync(x => x.Id == request.CourseId, ct);
            if (course.Status != CourseStatus.Approved)
                return Result.Failure("CANCELLATION_NOT_ALLOWED");
            DateTimeOffset now = clock.UtcNow;
            request.Status = approve ? RequestStatus.Approved : RequestStatus.Rejected;
            request.DecidedBy = actorUserId; request.DecidedAt = now;
            request.DecisionNotes = notes?.Trim();
            request.UpdatedAt = now; request.UpdatedByUserId = actorUserId;
            if (approve) await CancelAsync(course, actorUserId,
                request.Reason, now, ct);
            Notify([request.RequestedBy], NotificationType.CancellationDecision,
                approve ? "الموافقة على إلغاء الدورة" : "رفض طلب إلغاء الدورة",
                approve ? "Cancellation approved" : "Cancellation rejected",
                course, now);
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "cancellation.decide", "قرار طلب إلغاء", "Cancellation decision",
                "Course", course.Id, course.TitleAr,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(new
                { approve, notes }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> CancelDirectAsync(long actorUserId, long courseId,
        string reason, long letterFileId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "course.cancel_direct", ct) ||
            !await IsQualityOrAdminAsync(actorUserId, ct))
            return Result.Failure("FORBIDDEN");
        if (!ValidReason(reason)) return Result.Failure("CANCELLATION_REASON_REQUIRED");
        if (!await ValidLetterAsync(actorUserId, letterFileId, ct))
            return Result.Failure("CANCELLATION_LETTER_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Status != CourseStatus.Approved)
                return Result.Failure("CANCELLATION_NOT_ALLOWED");
            DateTimeOffset now = clock.UtcNow;
            await LinkLetterAsync(course, letterFileId, actorUserId, now, ct);
            await CancelAsync(course, actorUserId, reason.Trim(), now, ct);
            var pending = await db.CourseCancellationRequests.Where(x =>
                x.CourseId == courseId && x.Status == RequestStatus.Pending)
                .ToListAsync(ct);
            foreach (var request in pending)
            {
                request.Status = RequestStatus.Approved;
                request.DecidedBy = actorUserId; request.DecidedAt = now;
                request.DecisionNotes = "Cancelled directly by quality";
            }
            Notify(pending.Select(x => x.RequestedBy).Distinct(),
                NotificationType.CancellationDecision,
                "الموافقة على إلغاء الدورة", "Cancellation approved", course, now);
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "course.cancel_direct", "إلغاء دورة مباشرة", "Course cancelled directly",
                "Course", course.Id, course.TitleAr,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(new
                { reason = reason.Trim() }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task CancelAsync(Course course, long actorUserId,
        string reason, DateTimeOffset now, CancellationToken ct)
    {
        course.Status = CourseStatus.Cancelled;
        course.StatusChangedAt = now;
        course.CancelledAt = now; course.CancelledBy = actorUserId;
        course.CancellationReason = reason;
        course.IsArchived = true; course.ArchivedAt = now;
        course.ArchivedByUserId = actorUserId;
        course.UpdatedAt = now; course.UpdatedByUserId = actorUserId;
        db.CourseStatusHistorys.Add(new CourseStatusHistory
        {
            CourseId = course.Id,
            FromStatus = CourseStatus.Approved,
            ToStatus = CourseStatus.Cancelled,
            ChangedBy = actorUserId,
            ChangedAt = now,
            Reason = reason
        });
        var certificates = await db.Certificates.Where(x => x.CourseId == course.Id &&
            x.Status == CertificateStatus.Issued).ToListAsync(ct);
        foreach (var certificate in certificates)
        {
            certificate.Status = CertificateStatus.Revoked;
            certificate.RevokedAt = now; certificate.RevokedBy = actorUserId;
            certificate.RevokedReason = CertificateRevokeReason.CourseCancelled;
            certificate.RevokedNote = reason;
        }
        var recipients = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == course.Id && x.IsActive)
            .Select(x => x.UserId).ToListAsync(ct);
        if (course.InstructorUserId is long instructor) recipients.Add(instructor);
        if (course.ProposedByUserId is long dean) recipients.Add(dean);
        Notify(recipients.Distinct(), NotificationType.CourseCancelled,
            "إلغاء دورة", "Course cancelled", course, now);
    }

    private async Task LinkLetterAsync(Course course, long fileId,
        long actorUserId, DateTimeOffset now, CancellationToken ct)
    {
        db.CourseFiles.Add(new CourseFile
        {
            CourseId = course.Id,
            FileId = fileId,
            Purpose = "CancellationLetter",
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = actorUserId
        });
        var vault = await db.VaultDocuments.SingleOrDefaultAsync(x =>
            x.FileId == fileId && x.IsActive, ct);
        if (vault is not null)
        {
            vault.RelatedCourseId = course.Id;
            vault.RelatedCenterId = course.ProposingCenterId;
        }
    }

    private Task<bool> ValidLetterAsync(long actorUserId, long fileId,
        CancellationToken ct) => db.StoredFiles.AnyAsync(x => x.Id == fileId &&
            x.UploadedBy == actorUserId && x.IsActive &&
            x.Kind == FileKind.CancellationLetter && x.MimeType == "application/pdf", ct);

    private static bool ValidReason(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 1000;

    private async Task<bool> CanDecideAsync(long actorUserId, CancellationToken ct) =>
        await permissions.HasAsync(actorUserId, "cancellation.decide", ct) &&
        await IsQualityOrAdminAsync(actorUserId, ct);

    private async Task<bool> IsQualityOrAdminAsync(long actorUserId,
        CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        return effective.HasMode(RoleCode.Quality) || effective.HasMode(RoleCode.Admin);
    }

    private void Notify(IEnumerable<long> users, NotificationType type,
        string titleAr, string titleEn, Course course, DateTimeOffset now)
    {
        foreach (long userId in users)
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
}
