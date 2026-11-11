using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface IEvaluationQueryService
{
    /// <summary>Results for one course: per-question option counts, shares and average score.</summary>
    Task<Result<CourseEvaluationResultsDto>> CourseResultsAsync(long actorUserId,
        long courseId, CancellationToken ct);
    Task<Result> ExtendPeriodAsync(long actorUserId, long courseId,
        DateOnly newEndDate, CancellationToken ct);
}

public sealed record CourseEvaluationResultsDto(long CourseId, string CourseNo,
    string CourseTitleAr, string InstructorNameAr, long VersionId,
    int VersionNo, int Responses,
    IReadOnlyList<EvaluationQuestionResultDto> Questions);

public sealed record EvaluationQuestionResultDto(long QuestionId, Guid QuestionKey,
    string TextAr, double? AverageScore,
    IReadOnlyList<EvaluationOptionResultDto> Options);

public sealed record EvaluationOptionResultDto(long OptionId, string LabelAr,
    short? Score, int Count, double SharePercent);

public sealed class EvaluationQueryService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IAuditLogger audit,
    IEvaluationPinningService pinning) : IEvaluationQueryService
{
    public async Task<Result<CourseEvaluationResultsDto>> CourseResultsAsync(
        long actorUserId, long courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => new
            {
                x.Id,
                x.CourseNo,
                x.TitleAr,
                x.InstructorNameAr,
                x.InstructorUserId,
                x.ManagedByQuality,
                x.EvaluationFormVersionId
            })
            .SingleOrDefaultAsync(ct);
        if (course is null) return Result<CourseEvaluationResultsDto>.Failure("COURSE_NOT_FOUND");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool admin = effective.HasMode(RoleCode.Admin) &&
            effective.Keys.Contains("evaluation.results.all");
        bool quality = effective.HasMode(RoleCode.Quality) && course.ManagedByQuality &&
            effective.Keys.Contains("evaluation.results.all");
        bool instructor = (effective.HasMode(RoleCode.Instructor) ||
            effective.HasMode(RoleCode.ExternalInstructor)) &&
            course.InstructorUserId == actorUserId &&
            effective.Keys.Contains("evaluation.results.course");
        if (!admin && !quality && !instructor)
            return Result<CourseEvaluationResultsDto>.Failure("FORBIDDEN");
        long? versionId = course.EvaluationFormVersionId ??
            await db.EvaluationFormVersions.AsNoTracking()
                .Where(x => x.Status == FormVersionStatus.Published)
                .OrderByDescending(x => x.VersionNo)
                .Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        if (versionId is null)
            return Result<CourseEvaluationResultsDto>.Failure("EVAL_FORM_MISSING");
        var version = await db.EvaluationFormVersions.AsNoTracking()
            .Where(x => x.Id == versionId)
            .Select(x => new { x.VersionNo }).SingleAsync(ct);
        var responses = await db.EvaluationResponses.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.VersionId == versionId)
            .Select(x => x.Id).ToListAsync(ct);
        var answers = await db.EvaluationAnswers.AsNoTracking()
            .Where(x => x.Response!.CourseId == courseId && x.Response.VersionId == versionId)
            .Select(x => new { x.QuestionId, x.OptionId }).ToListAsync(ct);
        var questions = await db.EvaluationQuestions.AsNoTracking()
            .Where(x => x.VersionId == versionId).OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.QuestionKey, x.TextAr })
            .ToListAsync(ct);
        var options = await db.EvaluationOptions.AsNoTracking()
            .Where(x => x.Question!.VersionId == versionId).OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.QuestionId, x.LabelAr, x.Score })
            .ToListAsync(ct);
        var questionResults = new List<EvaluationQuestionResultDto>();
        foreach (var question in questions)
        {
            int answered = answers.Count(a => a.QuestionId == question.Id);
            var optionResults = options.Where(o => o.QuestionId == question.Id)
                .Select(o => new EvaluationOptionResultDto(o.Id, o.LabelAr, o.Score,
                    answers.Count(a => a.OptionId == o.Id),
                    answered == 0 ? 0 : Math.Round(
                        answers.Count(a => a.OptionId == o.Id) * 100.0 / answered, 1)))
                .ToList();
            var scored = answers.Where(a => a.QuestionId == question.Id)
                .Join(options.Where(o => o.QuestionId == question.Id &&
                    o.Score.HasValue), a => a.OptionId, o => o.Id,
                    (_, o) => (double)o.Score!.Value).ToList();
            questionResults.Add(new(question.Id, question.QuestionKey,
                question.TextAr, scored.Count == 0 ? null :
                    Math.Round(scored.Average(), 2), optionResults));
        }
        return Result<CourseEvaluationResultsDto>.Success(new CourseEvaluationResultsDto(
            courseId, course.CourseNo, course.TitleAr, course.InstructorNameAr,
            versionId.Value, version.VersionNo,
            responses.Count, questionResults));
    }

    public async Task<Result> ExtendPeriodAsync(long actorUserId, long courseId,
        DateOnly newEndDate, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "evaluation.extend_period", ct))
            return Result.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool admin = effective.HasMode(RoleCode.Admin);
        if (!admin) return Result.Failure("FORBIDDEN");
        DateTimeOffset now = clock.UtcNow;
        var result = await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (!ScopeGuard.AllowsQualityManagedCourse(effective, course.ManagedByQuality))
                return Result.Failure("FORBIDDEN");
            if (course.EvaluationEndDate is null)
                return Result.Failure("EVAL_PERIOD_NOT_SET");
            if (!admin && newEndDate <= course.EvaluationEndDate)
                return Result.Failure("EVAL_PERIOD_NOT_LATER");
            if (!admin && newEndDate < clock.TodayAmman)
                return Result.Failure("EVAL_PERIOD_INVALID");
            var before = new { course.EvaluationEndDate };
            course.EvaluationEndDate = newEndDate;
            course.UpdatedAt = now; course.UpdatedByUserId = actorUserId;
            await audit.LogAsync(new AuditEntry(AuditCategory.Evaluation,
                "evaluation.period_extended", "تمديد فترة التقييم",
                "Evaluation period extended", "Course", courseId, course.TitleAr,
                BeforeJson: System.Text.Json.JsonSerializer.Serialize(before),
                AfterJson: System.Text.Json.JsonSerializer.Serialize(
                    new { evaluationEndDate = newEndDate }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
        if (result.Ok) await pinning.PinCompletedAsync(ct);
        return result;
    }
}
