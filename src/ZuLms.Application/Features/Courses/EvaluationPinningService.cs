using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class EvaluationPinningService(IAppDbContext db, IClock clock,
    IEligibilitySnapshotService eligibility, IAuditLogger audit)
    : IEvaluationPinningService
{
    public async Task<int> PinCompletedAsync(CancellationToken ct)
    {
        var courseIds = await db.Courses.AsNoTracking()
            .Where(x => x.Status == CourseStatus.Finished && x.IsActive &&
                x.EvaluationFormVersionId == null &&
                x.EvaluationEndDate != null &&
                x.EvaluationEndDate < clock.TodayAmman)
            .Select(x => x.Id).ToListAsync(ct);
        int pinned = 0;
        foreach (long courseId in courseIds)
        {
            bool changed = await db.InSerializableTransactionAsync(async () =>
            {
                var course = await db.Courses.SingleOrDefaultAsync(x =>
                    x.Id == courseId && x.Status == CourseStatus.Finished &&
                    x.IsActive && x.EvaluationFormVersionId == null &&
                    x.EvaluationEndDate < clock.TodayAmman, ct);
                if (course is null) return false;
                long? versionId = await db.EvaluationFormVersions.AsNoTracking()
                    .Where(x => x.Status == FormVersionStatus.Published)
                    .OrderByDescending(x => x.VersionNo)
                    .Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
                if (versionId is null) return false;
                var participants = await db.CourseParticipants.AsNoTracking()
                    .Where(x => x.CourseId == courseId && x.IsActive)
                    .Select(x => x.Id).ToListAsync(ct);
                foreach (long participantId in participants)
                {
                    var state = await eligibility.EvaluateAsync(courseId,
                        participantId, false, ct);
                    if (!state.Ok) return false;
                    if (!state.Value!.SurveyOpen) continue;
                    bool answered = await db.EvaluationResponses.AnyAsync(x =>
                        x.CourseId == courseId &&
                        x.ParticipantId == participantId &&
                        x.VersionId == versionId, ct);
                    if (!answered) return false;
                }
                course.EvaluationFormVersionId = versionId;
                course.UpdatedAt = clock.UtcNow;
                await audit.LogAsync(new AuditEntry(AuditCategory.Evaluation,
                    "evaluation.pin", "تثبيت نسخة تقييم الدورة",
                    "Course evaluation version pinned", "Course",
                    courseId, course.TitleAr, CourseId: courseId), ct);
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);
            if (changed) pinned++;
        }
        return pinned;
    }
}
