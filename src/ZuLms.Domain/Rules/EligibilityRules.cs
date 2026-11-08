using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Rules;

public enum ExamState { None, NotRecorded, Passed, Failed }

public sealed record EligibilityInput(
    CourseStatus Status, bool HasExam, int TotalLectures, int Attended, int ThresholdPercent,
    ExamState Exam, bool SurveyCompleted, bool ParticipantActive, bool CourseActive);

public sealed record EligibilityResult(
    bool Eligible, bool AttendanceOk, bool ExamOk, bool SurveyOpen, bool ImpactOpen,
    IReadOnlyList<string> UnmetReasons);

public static class EligibilityResultExtensions
{
    public static bool CourseConditionsMet(this EligibilityResult result) =>
        !result.UnmetReasons.Contains("COURSE_NOT_FINISHED") &&
        !result.UnmetReasons.Contains("PARTICIPANT_INACTIVE");
}

public static class EligibilityRules
{
    public static EligibilityResult Evaluate(EligibilityInput i)
    {
        bool attendanceOk = AttendanceRules.IsSatisfied(i.Attended, i.TotalLectures, i.ThresholdPercent);
        bool examOk = !i.HasExam || i.Exam == ExamState.Passed;
        bool open = i.Status == CourseStatus.Finished && attendanceOk && examOk;
        bool eligible = open && i.CourseActive && i.ParticipantActive && i.SurveyCompleted;
        var reasons = new List<string>();
        if (i.Status != CourseStatus.Finished || !i.CourseActive) reasons.Add("COURSE_NOT_FINISHED");
        if (!i.ParticipantActive) reasons.Add("PARTICIPANT_INACTIVE");
        if (!attendanceOk) reasons.Add("ATTENDANCE_INSUFFICIENT");
        if (!examOk) reasons.Add("EXAM_NOT_PASSED");
        if (!i.SurveyCompleted) reasons.Add("EVALUATION_MISSING");
        return new EligibilityResult(eligible, attendanceOk, examOk, open, open, reasons);
    }
}
