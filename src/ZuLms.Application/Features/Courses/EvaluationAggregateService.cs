using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface IEvaluationAggregateService
{
    Task<Result<EvaluationAggregateDto>> GetAsync(long actorId,
        EvaluationAggregateFilter filter, CancellationToken ct);
    Task<Result<IReadOnlyList<EvaluationVersionOptionDto>>> VersionsAsync(
        long actorId, CancellationToken ct);
}

public sealed record EvaluationVersionOptionDto(long Id, int VersionNo,
    string TitleAr);

public sealed record EvaluationAggregateFilter(long? CourseId, long? CenterId,
    long? TrackId, DateOnly? FromDate, DateOnly? ToDate, long? VersionId);

public sealed record EvaluationAggregateDto(int Courses, int Responses,
    IReadOnlyList<EvaluationAggregateQuestionDto> Questions);

public sealed record EvaluationAggregateQuestionDto(Guid QuestionKey,
    string TextAr, IReadOnlyList<int> VersionNumbers, int Answers,
    double? AverageScore, IReadOnlyList<EvaluationAggregateOptionDto> Options);

public sealed record EvaluationAggregateOptionDto(string LabelAr,
    short? Score, int Count, double SharePercent);

public sealed class EvaluationAggregateService(IAppDbContext db,
    IPermissionService permissions) : IEvaluationAggregateService
{
    public async Task<Result<IReadOnlyList<EvaluationVersionOptionDto>>>
        VersionsAsync(long actorId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "evaluation.results.all", ct))
            return Result<IReadOnlyList<EvaluationVersionOptionDto>>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        if (!effective.HasMode(RoleCode.Admin) && !effective.HasMode(RoleCode.Quality))
            return Result<IReadOnlyList<EvaluationVersionOptionDto>>.Failure("FORBIDDEN");
        var versions = await db.EvaluationFormVersions.AsNoTracking()
            .Where(x => x.Status == FormVersionStatus.Published)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new EvaluationVersionOptionDto(x.Id,
                x.VersionNo, x.TitleAr)).ToListAsync(ct);
        return Result<IReadOnlyList<EvaluationVersionOptionDto>>.Success(versions);
    }

    public async Task<Result<EvaluationAggregateDto>> GetAsync(long actorId,
        EvaluationAggregateFilter filter, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "evaluation.results.all", ct))
            return Result<EvaluationAggregateDto>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        if (!effective.HasMode(RoleCode.Admin) && !effective.HasMode(RoleCode.Quality))
            return Result<EvaluationAggregateDto>.Failure("FORBIDDEN");
        if (filter.CourseId is <= 0 || filter.CenterId is < 0 ||
            filter.TrackId is <= 0 || filter.VersionId is <= 0 ||
            filter.FromDate > filter.ToDate)
            return Result<EvaluationAggregateDto>.Failure("QUERY_INVALID");

        long? latestVersion = await db.EvaluationFormVersions.AsNoTracking()
            .Where(x => x.Status == FormVersionStatus.Published)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        if (latestVersion is null)
            return Result<EvaluationAggregateDto>.Failure("EVAL_FORM_MISSING");
        var query = db.Courses.AsNoTracking()
            .Where(x => x.Status == CourseStatus.Finished);
        if (effective.HasMode(RoleCode.Quality))
            query = query.Where(x => x.ManagedByQuality);
        if (filter.CourseId is long courseId)
            query = query.Where(x => x.Id == courseId);
        if (filter.CenterId == 0)
            query = query.Where(x => x.AudienceType == AudienceType.Administrative);
        else if (filter.CenterId is long centerId)
            query = query.Where(x => x.ProposingCenterId == centerId ||
                x.TargetCenterId == centerId);
        if (filter.TrackId is long trackId)
            query = query.Where(x => x.TrackId == trackId);
        if (filter.FromDate is DateOnly from)
            query = query.Where(x => x.PlannedStartDate >= from);
        if (filter.ToDate is DateOnly to)
            query = query.Where(x => x.PlannedStartDate <= to);
        var courses = await query.Select(x => new
        {
            x.Id,
            VersionId = x.EvaluationFormVersionId ?? latestVersion.Value
        }).ToListAsync(ct);
        if (filter.VersionId is long selectedVersion)
            courses = courses.Where(x => x.VersionId == selectedVersion).ToList();
        if (courses.Count == 0)
            return Result<EvaluationAggregateDto>.Success(new(0, 0, []));
        var versionsByCourse = courses.ToDictionary(x => x.Id, x => x.VersionId);
        var courseIds = versionsByCourse.Keys.ToList();
        var versionIds = versionsByCourse.Values.Distinct().ToList();
        var versionNumbers = await db.EvaluationFormVersions.AsNoTracking()
            .Where(x => versionIds.Contains(x.Id))
            .Select(x => new { x.Id, x.VersionNo })
            .ToDictionaryAsync(x => x.Id, x => x.VersionNo, ct);
        var questions = await db.EvaluationQuestions.AsNoTracking()
            .Where(x => versionIds.Contains(x.VersionId))
            .Select(x => new
            {
                x.Id,
                x.VersionId,
                x.QuestionKey,
                x.TextAr,
                x.SortOrder
            }).ToListAsync(ct);
        var questionIds = questions.Select(x => x.Id).ToList();
        var options = await db.EvaluationOptions.AsNoTracking()
            .Where(x => questionIds.Contains(x.QuestionId))
            .Select(x => new
            {
                x.Id,
                x.QuestionId,
                x.SortOrder,
                x.LabelAr,
                x.LabelEn,
                x.Score
            }).ToListAsync(ct);
        var responses = await db.EvaluationResponses.AsNoTracking()
            .Where(x => courseIds.Contains(x.CourseId))
            .Select(x => new { x.Id, x.CourseId, x.VersionId })
            .ToListAsync(ct);
        var selectedResponses = responses.Where(x =>
            versionsByCourse[x.CourseId] == x.VersionId).ToList();
        var responseIds = selectedResponses.Select(x => x.Id).ToList();
        var answers = await db.EvaluationAnswers.AsNoTracking()
            .Where(x => responseIds.Contains(x.ResponseId))
            .Select(x => new { x.QuestionId, x.OptionId }).ToListAsync(ct);

        var optionLists = options.GroupBy(x => x.QuestionId)
            .ToDictionary(x => x.Key, x => x.OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Id).ToList());
        var signatures = optionLists.ToDictionary(x => x.Key,
            x => JsonSerializer.Serialize(x.Value.Select(o => new
            {
                o.SortOrder,
                o.LabelAr,
                o.LabelEn,
                o.Score
            })));
        var report = new List<EvaluationAggregateQuestionDto>();
        foreach (var group in questions.GroupBy(q => (q.QuestionKey,
            signatures.GetValueOrDefault(q.Id, "[]"))))
        {
            var representative = group.OrderByDescending(q =>
                versionNumbers[q.VersionId]).ThenBy(q => q.SortOrder).First();
            var referenceOptions = optionLists.GetValueOrDefault(representative.Id);
            if (referenceOptions is null) continue;
            var counts = new int[referenceOptions.Count];
            var optionIndex = new Dictionary<long, int>();
            foreach (var question in group)
            {
                if (!optionLists.TryGetValue(question.Id, out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                    optionIndex[list[i].Id] = i;
            }
            int answered = 0;
            double scoreTotal = 0;
            int scored = 0;
            foreach (var answer in answers)
            {
                if (!optionIndex.TryGetValue(answer.OptionId, out int index))
                    continue;
                counts[index]++;
                answered++;
                if (referenceOptions[index].Score is short score)
                {
                    scoreTotal += score;
                    scored++;
                }
            }
            var rows = referenceOptions.Select((option, index) =>
                new EvaluationAggregateOptionDto(option.LabelAr, option.Score,
                    counts[index], answered == 0 ? 0 :
                        Math.Round(counts[index] * 100.0 / answered, 1))).ToList();
            report.Add(new EvaluationAggregateQuestionDto(group.Key.QuestionKey,
                representative.TextAr, group.Select(q =>
                    versionNumbers[q.VersionId]).Distinct().Order().ToList(),
                answered, scored == 0 ? null :
                    Math.Round(scoreTotal / scored, 2), rows));
        }
        return Result<EvaluationAggregateDto>.Success(new(courses.Count,
            selectedResponses.Count, report.OrderBy(x =>
                x.VersionNumbers.FirstOrDefault()).ThenBy(x => x.TextAr).ToList()));
    }

}
