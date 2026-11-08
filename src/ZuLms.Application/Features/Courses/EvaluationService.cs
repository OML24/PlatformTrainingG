using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Evaluation;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class EvaluationService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilitySnapshotService eligibility,
    IEligibilityRecalculationService recalculation, IAuditLogger audit)
    : IEvaluationService
{
    public async Task<Result<EvaluationFormDto>> GetFormAsync(long actorId,
        long courseId, CancellationToken ct)
    {
        var access = await AccessAsync(actorId, courseId, ct);
        if (!access.Ok) return Result<EvaluationFormDto>.Failure(access.Error!.Code);
        var (participantId, versionId, courseTitle, instructorName, endDate) =
            access.Value!;
        if (await db.EvaluationResponses.AnyAsync(x =>
            x.CourseId == courseId && x.ParticipantId == participantId &&
            x.VersionId == versionId, ct))
            return Result<EvaluationFormDto>.Failure("EVAL_ALREADY_SUBMITTED");
        var questions = await db.EvaluationQuestions.AsNoTracking()
            .Where(x => x.VersionId == versionId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new EvaluationQuestionDto(x.Id, x.TextAr,
                db.EvaluationOptions.Where(o => o.QuestionId == x.Id)
                    .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                    .Select(o => new EvaluationOptionDto(o.Id, o.LabelAr))
                    .ToList())).ToListAsync(ct);
        if (questions.Count == 0)
            return Result<EvaluationFormDto>.Failure("EVAL_FORM_EMPTY");
        return Result<EvaluationFormDto>.Success(new(versionId,
            courseTitle, instructorName, endDate, questions));
    }

    public async Task<Result> SubmitAsync(long actorId, long courseId,
        IReadOnlyList<EvaluationAnswerInput> answers, CancellationToken ct)
    {
        if (answers is null) return Result.Failure("EVAL_ANSWERS_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var access = await AccessAsync(actorId, courseId, ct);
            if (!access.Ok) return Result.Failure(access.Error!.Code);
            var (participantId, versionId, _, _, _) = access.Value!;
            if (await db.EvaluationResponses.AnyAsync(x =>
                x.CourseId == courseId && x.ParticipantId == participantId &&
                x.VersionId == versionId, ct))
                return Result.Failure("EVAL_ALREADY_SUBMITTED");
            var questions = await db.EvaluationQuestions.AsNoTracking()
                .Where(x => x.VersionId == versionId)
                .Select(x => x.Id).ToListAsync(ct);
            if (questions.Count == 0 || answers.Count != questions.Count ||
                answers.Select(x => x.QuestionId).Distinct().Count() != questions.Count ||
                answers.Any(x => !questions.Contains(x.QuestionId)))
                return Result.Failure("EVAL_ANSWERS_INVALID");
            var optionIds = await db.EvaluationOptions.AsNoTracking()
                .Where(x => questions.Contains(x.QuestionId))
                .Select(x => new { x.Id, x.QuestionId }).ToListAsync(ct);
            if (answers.Any(a => !optionIds.Any(o =>
                o.Id == a.OptionId && o.QuestionId == a.QuestionId)))
                return Result.Failure("EVAL_ANSWERS_INVALID");
            var response = new EvaluationResponse
            {
                CourseId = courseId, ParticipantId = participantId,
                VersionId = versionId, SubmittedAt = clock.UtcNow
            };
            db.EvaluationResponses.Add(response);
            await db.SaveChangesAsync(ct);
            foreach (var answer in answers)
                db.EvaluationAnswers.Add(new EvaluationAnswer
                {
                    ResponseId = response.Id,
                    QuestionId = answer.QuestionId,
                    OptionId = answer.OptionId
                });
            await db.SaveChangesAsync(ct);
            var recalc = await recalculation.RecalculateParticipantAsync(
                courseId, participantId, null, actorId, ct);
            if (!recalc.Ok) return Result.Failure(recalc.Error!.Code);
            await audit.LogAsync(new AuditEntry(AuditCategory.Evaluation,
                "evaluation.submit", "إرسال تقييم الدورة",
                "Course evaluation submitted", "EvaluationResponse",
                response.Id, CourseId: courseId, ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<Result<(long ParticipantId, long VersionId,
        string CourseTitle, string InstructorName, DateOnly EndDate)>>
        AccessAsync(long actorId, long courseId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "evaluation.submit", ct))
            return Result<(long, long, string, string, DateOnly)>.Failure("FORBIDDEN");
        var participant = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.UserId == actorId &&
                x.IsActive).Select(x => new { x.Id, x.AdminCertificateOverride })
            .SingleOrDefaultAsync(ct);
        if (participant is null)
            return Result<(long, long, string, string, DateOnly)>.Failure("FORBIDDEN");
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == courseId, ct);
        if (course is null)
            return Result<(long, long, string, string, DateOnly)>
                .Failure("COURSE_NOT_FOUND");
        var state = await eligibility.EvaluateAsync(courseId,
            participant.Id, false, ct);
        if (!state.Ok)
            return Result<(long, long, string, string, DateOnly)>
                .Failure(state.Error!.Code);
        if (!state.Value!.SurveyOpen || !participant.AdminCertificateOverride &&
            (course.EvaluationEndDate is null ||
             clock.TodayAmman > course.EvaluationEndDate.Value))
            return Result<(long, long, string, string, DateOnly)>
                .Failure("EVAL_CLOSED");
        long? versionId = course.EvaluationFormVersionId ??
            await db.EvaluationFormVersions.AsNoTracking()
                .Where(x => x.Status == FormVersionStatus.Published)
                .OrderByDescending(x => x.VersionNo)
                .Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        if (versionId is null)
            return Result<(long, long, string, string, DateOnly)>
                .Failure("EVAL_FORM_MISSING");
        return Result<(long, long, string, string, DateOnly)>.Success((
            participant.Id, versionId.Value, course.TitleAr,
            course.InstructorNameAr, course.EvaluationEndDate ?? clock.TodayAmman));
    }
}
