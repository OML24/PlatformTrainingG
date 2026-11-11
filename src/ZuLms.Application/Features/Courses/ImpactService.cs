using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class ImpactService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilitySnapshotService eligibility,
    IEligibilityRecalculationService recalculation, IAuditLogger audit)
    : IImpactService
{
    public async Task<Result<IReadOnlyList<ImpactDto>>> MineAsync(long actorId,
        long courseId, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        if (!ScopeGuard.AllowsPersonalRead(effective, "impact.submit"))
            return Result<IReadOnlyList<ImpactDto>>.Failure("FORBIDDEN");
        var participant = await db.CourseParticipants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CourseId == courseId &&
                x.UserId == actorId && x.IsActive, ct);
        if (participant is null)
            return Result<IReadOnlyList<ImpactDto>>.Failure("FORBIDDEN");
        var rows = await Project(db.ImpactSubmissions.AsNoTracking()
            .Where(x => x.ParticipantId == participant.Id)
            .OrderByDescending(x => x.SequenceNo)).ToListAsync(ct);
        return Result<IReadOnlyList<ImpactDto>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<ImpactDto>>> PendingAsync(long actorId,
        long courseId, CancellationToken ct)
    {
        if (!await CanReviewAsync(actorId, courseId, ct))
            return Result<IReadOnlyList<ImpactDto>>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorId, ct)).HasMode(RoleCode.Admin);
        var rows = await Project(db.ImpactSubmissions.AsNoTracking()
            .Where(x => x.Participant!.CourseId == courseId && x.IsActive &&
                x.Status == ImpactStatus.Submitted &&
                (admin || x.Participant.User!.Employee != null &&
                    x.Participant.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
            .OrderBy(x => x.Id)).ToListAsync(ct);
        return Result<IReadOnlyList<ImpactDto>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<ImpactReviewCourseDto>>> ReviewCoursesAsync(
        long actorId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "impact.review", ct))
            return Result<IReadOnlyList<ImpactReviewCourseDto>>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        bool admin = effective.HasMode(RoleCode.Admin);
        bool quality = effective.HasMode(RoleCode.Quality);
        bool dean = effective.IsUnitHead;
        if (!admin && !quality && !dean)
            return Result<IReadOnlyList<ImpactReviewCourseDto>>.Failure("FORBIDDEN");
        long[] deanCenters = dean ? effective.DeanScopeCenterIds.ToArray() : [];
        var candidates = await db.Courses.AsNoTracking()
            .Where(x => db.ImpactSubmissions.Any(i => i.Participant!.CourseId == x.Id &&
                i.IsActive && i.Status == ImpactStatus.Submitted))
            .OrderByDescending(x => x.PlannedEndDate).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.CourseNo,
                x.TitleAr,
                x.Origin,
                x.AudienceType,
                x.ProposingCenterId,
                x.TargetCenterId,
                x.ManagedByQuality,
                PendingCount = db.ImpactSubmissions.Count(i =>
                    i.Participant!.CourseId == x.Id && i.IsActive &&
                    i.Status == ImpactStatus.Submitted)
            }).ToListAsync(ct);
        var rows = candidates.Where(x => ImpactReviewPolicy.CanReview(admin,
                quality, dean, deanCenters, x.Origin, x.AudienceType,
                x.ProposingCenterId, x.TargetCenterId, x.ManagedByQuality))
            .Select(x => new ImpactReviewCourseDto(x.Id, x.CourseNo, x.TitleAr,
                ImpactReviewPolicy.IsDeanDestination(x.Origin, x.AudienceType,
                    x.ProposingCenterId, x.TargetCenterId, x.ManagedByQuality)
                    ? "Dean" : "Quality", x.PendingCount))
            .ToList();
        return Result<IReadOnlyList<ImpactReviewCourseDto>>.Success(rows);
    }

    public async Task<Result<long>> SubmitAsync(long actorId, long courseId,
        ImpactType type, long fileId, CancellationToken ct)
    {
        if (!Enum.IsDefined(type))
            return Result<long>.Failure("IMPACT_TYPE_INVALID");
        if (!await permissions.HasAsync(actorId, "impact.submit", ct))
            return Result<long>.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var participant = await db.CourseParticipants.Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.CourseId == courseId &&
                    x.UserId == actorId && x.IsActive, ct);
            if (participant?.Course is null)
                return Result<long>.Failure("FORBIDDEN");
            var state = await eligibility.EvaluateAsync(courseId,
                participant.Id, false, ct);
            if (!state.Ok) return Result<long>.Failure(state.Error!.Code);
            if (!state.Value!.ImpactOpen)
                return Result<long>.Failure("IMPACT_CLOSED");
            if (participant.Course.ImpactEndDate is DateOnly impactEnd &&
                clock.TodayAmman > impactEnd)
                return Result<long>.Failure("IMPACT_CLOSED");
            var file = await db.StoredFiles.SingleOrDefaultAsync(x =>
                x.Id == fileId && x.IsActive && x.Kind == FileKind.ImpactFile &&
                x.UploadedBy == actorId, ct);
            if (file is null) return Result<long>.Failure("FILE_NOT_FOUND");
            var previous = await db.ImpactSubmissions.IgnoreQueryFilters()
                .Where(x => x.ParticipantId == participant.Id)
                .OrderByDescending(x => x.SequenceNo).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (previous is not null && previous.Status != ImpactStatus.Returned)
                return Result<long>.Failure("IMPACT_ALREADY_SUBMITTED");
            if (await db.ImpactSubmissions.IgnoreQueryFilters().AnyAsync(x =>
                x.ParticipantId == participant.Id &&
                x.FileSha256 == file.Sha256, ct))
                return Result<long>.Failure("IMPACT_SAME_FILE");
            DateTimeOffset now = clock.UtcNow;
            var submission = new ImpactSubmission
            {
                ParticipantId = participant.Id,
                SequenceNo = (previous?.SequenceNo ?? 0) + 1,
                ImpactType = type,
                FileId = fileId,
                FileSha256 = file.Sha256,
                Status = ImpactStatus.Submitted,
                SubmittedAt = now,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = actorId
            };
            db.ImpactSubmissions.Add(submission);
            await db.SaveChangesAsync(ct);
            bool deanDestination = ImpactReviewPolicy.IsDeanDestination(
                participant.Course.Origin, participant.Course.AudienceType,
                participant.Course.ProposingCenterId, participant.Course.TargetCenterId,
                participant.Course.ManagedByQuality);
            var recipients = await db.UserRoles.AsNoTracking()
                .Where(x => x.IsActive && x.User!.IsActive &&
                    x.Role!.Code == RoleCode.Admin)
                .Select(x => x.UserId).ToListAsync(ct);
            if (deanDestination)
                recipients.AddRange(await db.UserRoles.AsNoTracking()
                    .Where(x => x.IsActive && (x.Role!.Code == RoleCode.Dean || x.Role!.Code == RoleCode.CenterManager) &&
                        x.ScopeCenterId == participant.Course.ProposingCenterId &&
                        x.User!.IsActive).Select(x => x.UserId).ToListAsync(ct));
            else
                recipients.AddRange(await db.UserRoles.AsNoTracking()
                    .Where(x => x.IsActive && x.Role!.Code == RoleCode.Quality &&
                        x.User!.IsActive).Select(x => x.UserId).ToListAsync(ct));
            foreach (long userId in recipients.Distinct().Where(x => x != actorId))
                db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.ImpactSubmitted,
                    TitleAr = "أثر تدريبي جديد",
                    TitleEn = "New training impact",
                    BodyAr = participant.Course.TitleAr,
                    BodyEn = participant.Course.TitleEn,
                    LinkUrl = $"/impact/review?courseId={courseId}",
                    RelatedEntityType = "ImpactSubmission",
                    RelatedEntityId = submission.Id,
                    CreatedAt = now,
                    IsActive = true
                });
            await audit.LogAsync(new AuditEntry(AuditCategory.Impact,
                "impact.submit", "رفع أثر تدريبي",
                "Training impact submitted", "ImpactSubmission",
                submission.Id, CourseId: courseId,
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(submission.Id);
        }, ct);
    }

    public async Task<Result> ReviewAsync(long actorId, long courseId,
        long submissionId, bool approve, string? notes, CancellationToken ct)
    {
        if (!await CanReviewAsync(actorId, courseId, ct))
            return Result.Failure("FORBIDDEN");
        if (!approve && string.IsNullOrWhiteSpace(notes))
            return Result.Failure("IMPACT_NOTES_REQUIRED");
        if (notes?.Trim().Length > 1000)
            return Result.Failure("REASON_TOO_LONG");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var submission = await db.ImpactSubmissions
                .Include(x => x.Participant).ThenInclude(x => x!.Course)
                .SingleOrDefaultAsync(x => x.Id == submissionId && x.IsActive &&
                    x.Participant!.CourseId == courseId, ct);
            if (submission?.Participant?.Course is null)
                return Result.Failure("IMPACT_NOT_FOUND");
            if (submission.Status != ImpactStatus.Submitted)
                return Result.Failure("IMPACT_REVIEW_CLOSED");
            DateTimeOffset now = clock.UtcNow;
            submission.Status = approve ? ImpactStatus.Approved :
                ImpactStatus.Returned;
            submission.InstructorNotes = string.IsNullOrWhiteSpace(notes)
                ? null : notes.Trim();
            submission.DecidedBy = actorId; submission.DecidedAt = now;
            submission.UpdatedAt = now; submission.UpdatedByUserId = actorId;
            await db.SaveChangesAsync(ct);
            var recalc = await recalculation.RecalculateParticipantAsync(
                courseId, submission.ParticipantId, null, actorId, ct);
            if (!recalc.Ok) return Result.Failure(recalc.Error!.Code);
            db.Notifications.Add(new Notification
            {
                UserId = submission.Participant.UserId,
                Type = NotificationType.ImpactDecision,
                TitleAr = approve ? "اعتماد الأثر التدريبي" : "إعادة الأثر للتعديل",
                TitleEn = approve ? "Training impact approved" :
                    "Training impact returned",
                BodyAr = submission.Participant.Course.TitleAr,
                BodyEn = submission.Participant.Course.TitleEn,
                LinkUrl = $"/courses/{courseId}?view=Member",
                RelatedEntityType = "ImpactSubmission",
                RelatedEntityId = submission.Id,
                CreatedAt = now,
                IsActive = true
            });
            await audit.LogAsync(new AuditEntry(AuditCategory.Impact,
                "impact.review", approve ? "اعتماد أثر تدريبي" :
                    "إعادة أثر للتعديل", approve ? "Training impact approved" :
                    "Training impact returned", "ImpactSubmission",
                submission.Id, CourseId: courseId,
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<bool> CanReviewAsync(long actorId, long courseId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "impact.review", ct))
            return false;
        var course = await db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == courseId, ct);
        if (course is null) return false;
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        if (effective.HasMode(RoleCode.Admin)) return true;
        bool deanDestination = ImpactReviewPolicy.IsDeanDestination(course.Origin,
            course.AudienceType, course.ProposingCenterId, course.TargetCenterId,
            course.ManagedByQuality);
        if (!deanDestination)
            return effective.HasMode(RoleCode.Quality);
        if (!effective.IsUnitHead ||
            course.ProposingCenterId is not long centerId) return false;
        return effective.DeanScopeCenterIds.Contains(centerId);
    }

    private static IQueryable<ImpactDto> Project(
        IQueryable<ImpactSubmission> query) => query.Select(x =>
            new ImpactDto(x.Id, x.ParticipantId,
                x.Participant!.User!.FullNameAr, x.ImpactType, x.Status,
                x.SequenceNo, x.FileId, x.InstructorNotes));
}
