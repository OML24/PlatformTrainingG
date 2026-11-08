using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class EligibilitySnapshotService(IAppDbContext db,
    ISettingsService settings) : IEligibilitySnapshotService
{
    public async Task<Result<EligibilityResult>> EvaluateAsync(long courseId,
        long participantId, bool treatCourseAsActive, CancellationToken ct)
    {
        var course = await db.Courses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => new { x.Id, x.Status, x.HasExam, x.IsActive,
                x.FinalLecturesCount, x.AttendanceThresholdPercentOverride,
                x.EvaluationFormVersionId }).SingleOrDefaultAsync(ct);
        if (course is null) return Result<EligibilityResult>.Failure("COURSE_NOT_FOUND");
        var participant = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.Id == participantId && x.CourseId == courseId)
            .Select(x => new { x.Id, x.IsActive, x.HistoricalPassed,
                x.AdminCertificateOverride, x.EvaluationExempt })
            .SingleOrDefaultAsync(ct);
        if (participant is null)
            return Result<EligibilityResult>.Failure("PARTICIPANT_NOT_FOUND");
        int threshold = course.AttendanceThresholdPercentOverride ??
            await settings.GetAsync<int>("attendance.threshold_percent", ct);
        int activeLectures = await db.Lectures.CountAsync(x =>
            x.CourseId == courseId && x.IsActive, ct);
        int total = course.Status == CourseStatus.Finished
            ? course.FinalLecturesCount ?? activeLectures : activeLectures;
        int attended = await db.AttendanceRecords.CountAsync(x =>
            x.ParticipantId == participantId && x.IsPresent &&
            x.Lecture!.CourseId == courseId && x.Lecture.IsActive, ct);
        ExamState exam = ExamState.None;
        if (course.HasExam)
        {
            var outcome = await db.ExamResults.AsNoTracking()
                .Where(x => x.ParticipantId == participantId && x.IsActive &&
                    x.Exam!.CourseId == courseId && x.Exam.IsActive)
                .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id)
                .Select(x => (ExamOutcome?)x.Outcome).FirstOrDefaultAsync(ct);
            exam = outcome switch
            {
                ExamOutcome.Passed => ExamState.Passed,
                ExamOutcome.Failed => ExamState.Failed,
                _ => ExamState.NotRecorded
            };
        }
        long? versionId = course.EvaluationFormVersionId ??
            await db.EvaluationFormVersions.AsNoTracking()
                .Where(x => x.Status == FormVersionStatus.Published)
                .OrderByDescending(x => x.VersionNo)
                .Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        bool currentSurvey = versionId is long selectedVersionId &&
            await db.EvaluationResponses.AnyAsync(x => x.ParticipantId == participantId &&
                x.CourseId == courseId && x.VersionId == selectedVersionId, ct);
        bool previouslyIssued = await db.Certificates.IgnoreQueryFilters().AnyAsync(x =>
            x.ParticipantId == participantId && x.Status == CertificateStatus.Issued, ct);
        bool survey = participant.EvaluationExempt || currentSurvey || previouslyIssued &&
            await db.EvaluationResponses.AnyAsync(x => x.ParticipantId == participantId &&
                x.CourseId == courseId, ct);
        var input = new EligibilityInput(course.Status, course.HasExam,
            total, attended, threshold, exam, survey, participant.IsActive,
            treatCourseAsActive || course.IsActive);
        var result = EligibilityRules.Evaluate(input);
        if (participant.AdminCertificateOverride)
        {
            // A force-issued certificate is an administrative correction of the
            // participant's complete record, not only a bypass during PDF creation.
            result = result with
            {
                Eligible = participant.IsActive && survey,
                AttendanceOk = true,
                ExamOk = true,
                SurveyOpen = true,
                ImpactOpen = false,
                UnmetReasons = survey ? [] : ["EVALUATION_MISSING"]
            };
        }
        else if (participant.HistoricalPassed == true)
        {
            var reasons = result.UnmetReasons.Where(x => x is not
                "ATTENDANCE_INSUFFICIENT" and not "EXAM_NOT_PASSED").ToList();
            bool eligible = result.CourseConditionsMet() && survey;
            result = result with { Eligible = eligible, AttendanceOk = true, ExamOk = true,
                SurveyOpen = true, ImpactOpen = true,
                UnmetReasons = reasons };
        }
        if (participant.HistoricalPassed == false)
            result = result with { Eligible = false, AttendanceOk = false, ExamOk = false,
                SurveyOpen = false, ImpactOpen = false,
                UnmetReasons = ["HISTORICAL_NOT_ELIGIBLE"] };
        return Result<EligibilityResult>.Success(result);
    }
}
