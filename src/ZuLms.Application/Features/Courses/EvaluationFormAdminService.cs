using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Evaluation;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class EvaluationFormAdminService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilitySnapshotService eligibility,
    IAuditLogger audit)
    : IEvaluationFormAdminService
{
    public async Task<Result<EvaluationDraftDto>> OpenDraftAsync(long actorId,
        CancellationToken ct)
    {
        if (!await CanEditAsync(actorId, ct))
            return Result<EvaluationDraftDto>.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var draft = await db.EvaluationFormVersions.SingleOrDefaultAsync(x =>
                x.Status == FormVersionStatus.Draft, ct);
            if (draft is null)
            {
                var published = await db.EvaluationFormVersions.AsNoTracking()
                    .Where(x => x.Status == FormVersionStatus.Published)
                    .OrderByDescending(x => x.VersionNo)
                    .FirstOrDefaultAsync(ct);
                int next = (await db.EvaluationFormVersions
                    .Select(x => (int?)x.VersionNo).MaxAsync(ct) ?? 0) + 1;
                draft = new EvaluationFormVersion
                {
                    VersionNo = next,
                    Status = FormVersionStatus.Draft,
                    TitleAr = published?.TitleAr ?? "نموذج تقييم التدريب",
                    TitleEn = published?.TitleEn,
                    CreatedAt = clock.UtcNow,
                    CreatedByUserId = actorId
                };
                db.EvaluationFormVersions.Add(draft);
                await db.SaveChangesAsync(ct);
                if (published is not null)
                {
                    var questions = await db.EvaluationQuestions.AsNoTracking()
                        .Where(x => x.VersionId == published.Id)
                        .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                        .ToListAsync(ct);
                    foreach (var old in questions)
                    {
                        var question = new EvaluationQuestion
                        {
                            VersionId = draft.Id,
                            QuestionKey = old.QuestionKey,
                            SortOrder = old.SortOrder,
                            TextAr = old.TextAr,
                            TextEn = old.TextEn
                        };
                        db.EvaluationQuestions.Add(question);
                        await db.SaveChangesAsync(ct);
                        var options = await db.EvaluationOptions.AsNoTracking()
                            .Where(x => x.QuestionId == old.Id)
                            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                            .ToListAsync(ct);
                        foreach (var option in options)
                            db.EvaluationOptions.Add(new EvaluationOption
                            {
                                QuestionId = question.Id,
                                SortOrder = option.SortOrder,
                                LabelAr = option.LabelAr,
                                LabelEn = option.LabelEn,
                                Score = option.Score
                            });
                    }
                    await db.SaveChangesAsync(ct);
                }
            }
            return Result<EvaluationDraftDto>.Success(await ReadAsync(draft, ct));
        }, ct);
    }

    public async Task<Result> SaveDraftAsync(long actorId, long draftId,
        EvaluationDraftInput input, CancellationToken ct)
    {
        if (!await CanEditAsync(actorId, ct))
            return Result.Failure("FORBIDDEN");
        if (input is null || string.IsNullOrWhiteSpace(input.TitleAr) ||
            input.TitleAr.Trim().Length > 300 || input.Questions is null ||
            input.Questions.Count == 0 || input.Questions.Count > 100 ||
            input.Questions.Where(q => q.ExistingId is not null)
                .Select(q => q.ExistingId).Distinct().Count() !=
                input.Questions.Count(q => q.ExistingId is not null) ||
            input.Questions.Any(q => string.IsNullOrWhiteSpace(q.TextAr) ||
                q.TextAr.Trim().Length > 1000 || q.Options is null ||
                q.Options.Count == 0 || q.Options.Count > 30 ||
                q.Options.Any(o => string.IsNullOrWhiteSpace(o.LabelAr) ||
                    o.LabelAr.Trim().Length > 300)))
            return Result.Failure("EVAL_FORM_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var draft = await db.EvaluationFormVersions.SingleOrDefaultAsync(x =>
                x.Id == draftId && x.Status == FormVersionStatus.Draft, ct);
            if (draft is null) return Result.Failure("EVAL_DRAFT_NOT_FOUND");
            var oldQuestions = await db.EvaluationQuestions
                .Where(x => x.VersionId == draftId).ToListAsync(ct);
            var oldIds = oldQuestions.Select(x => x.Id).ToList();
            var oldOptions = await db.EvaluationOptions
                .Where(x => oldIds.Contains(x.QuestionId)).ToListAsync(ct);
            db.EvaluationOptions.RemoveRange(oldOptions);
            await db.SaveChangesAsync(ct);
            db.EvaluationQuestions.RemoveRange(oldQuestions);
            await db.SaveChangesAsync(ct);
            draft.TitleAr = input.TitleAr.Trim();
            draft.UpdatedAt = clock.UtcNow;
            draft.UpdatedByUserId = actorId;
            for (int i = 0; i < input.Questions.Count; i++)
            {
                var source = input.Questions[i];
                var old = oldQuestions.FirstOrDefault(x =>
                    x.Id == source.ExistingId);
                var question = new EvaluationQuestion
                {
                    VersionId = draftId,
                    QuestionKey = old is not null && source.KeepQuestionKey
                        ? old.QuestionKey : Guid.NewGuid(),
                    SortOrder = i + 1,
                    TextAr = source.TextAr.Trim()
                };
                db.EvaluationQuestions.Add(question);
                await db.SaveChangesAsync(ct);
                for (int j = 0; j < source.Options.Count; j++)
                    db.EvaluationOptions.Add(new EvaluationOption
                    {
                        QuestionId = question.Id,
                        SortOrder = j + 1,
                        LabelAr = source.Options[j].LabelAr.Trim(),
                        Score = source.Options[j].Score
                    });
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Evaluation,
                "evalform.save_draft", "حفظ مسودة نموذج التقييم",
                "Evaluation draft saved", "EvaluationFormVersion",
                draftId, draft.TitleAr, ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result<int>> PublishAsync(long actorId, long draftId,
        CancellationToken ct)
    {
        if (!await CanEditAsync(actorId, ct))
            return Result<int>.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var draft = await db.EvaluationFormVersions.SingleOrDefaultAsync(x =>
                x.Id == draftId && x.Status == FormVersionStatus.Draft, ct);
            if (draft is null)
                return Result<int>.Failure("EVAL_DRAFT_NOT_FOUND");
            var questions = await db.EvaluationQuestions.AsNoTracking()
                .Where(x => x.VersionId == draftId)
                .Select(x => x.Id).ToListAsync(ct);
            if (questions.Count == 0 || await db.EvaluationOptions
                .Where(x => questions.Contains(x.QuestionId))
                .Select(x => x.QuestionId).Distinct().CountAsync(ct) !=
                questions.Count)
                return Result<int>.Failure("EVAL_FORM_EMPTY");
            draft.VersionNo = (await db.EvaluationFormVersions.AsNoTracking()
                .Where(x => x.Status == FormVersionStatus.Published)
                .Select(x => (int?)x.VersionNo).MaxAsync(ct) ?? 0) + 1;
            draft.Status = FormVersionStatus.Published;
            draft.PublishedAt = clock.UtcNow;
            draft.PublishedBy = actorId;
            await db.SaveChangesAsync(ct);
            var courses = await db.Courses.AsNoTracking()
                .Where(x => x.Status == CourseStatus.Finished && x.IsActive &&
                    x.EvaluationFormVersionId == null &&
                    x.EvaluationEndDate >= clock.TodayAmman)
                .Select(x => new { x.Id, x.TitleAr, x.TitleEn })
                .ToListAsync(ct);
            foreach (var course in courses)
            {
                var participants = await db.CourseParticipants.AsNoTracking()
                    .Where(x => x.CourseId == course.Id && x.IsActive)
                    .Select(x => new { x.Id, x.UserId }).ToListAsync(ct);
                foreach (var participant in participants)
                {
                    if (participant.UserId == actorId) continue;
                    var state = await eligibility.EvaluateAsync(course.Id,
                        participant.Id, false, ct);
                    if (!state.Ok || !state.Value!.SurveyOpen) continue;
                    db.Notifications.Add(new Notification
                    {
                        UserId = participant.UserId,
                        Type = NotificationType.EvaluationReopened,
                        TitleAr = "نسخة تقييم جديدة متاحة",
                        TitleEn = "New evaluation version available",
                        BodyAr = course.TitleAr,
                        BodyEn = course.TitleEn,
                        LinkUrl = $"/courses/{course.Id}?view=Member",
                        RelatedEntityType = "EvaluationFormVersion",
                        RelatedEntityId = draftId,
                        CreatedAt = clock.UtcNow,
                        IsActive = true
                    });
                }
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Evaluation,
                "evalform.publish", "نشر نموذج التقييم",
                "Evaluation form published", "EvaluationFormVersion",
                draftId, draft.TitleAr, ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result<int>.Success(draft.VersionNo);
        }, ct);
    }

    private async Task<EvaluationDraftDto> ReadAsync(
        EvaluationFormVersion draft, CancellationToken ct)
    {
        var questions = await db.EvaluationQuestions.AsNoTracking()
            .Where(x => x.VersionId == draft.Id)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new EvaluationDraftQuestionDto(x.Id,
                x.QuestionKey, x.TextAr,
                db.EvaluationOptions.Where(o => o.QuestionId == x.Id)
                    .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                    .Select(o => new EvaluationDraftOptionDto(o.Id,
                        o.LabelAr, o.Score)).ToList()))
            .ToListAsync(ct);
        return new EvaluationDraftDto(draft.Id, draft.VersionNo,
            draft.TitleAr, questions);
    }

    private async Task<bool> CanEditAsync(long actorId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "evalform.edit", ct))
            return false;
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        return effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality);
    }
}
