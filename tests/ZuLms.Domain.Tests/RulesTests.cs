using ZuLms.Domain.Enums;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Rules;

namespace ZuLms.Domain.Tests;

public sealed class RulesTests
{
    [Theory]
    [InlineData(10, 75, 8)]
    [InlineData(8, 75, 6)]
    [InlineData(4, 75, 3)]
    [InlineData(5, 75, 4)]
    [InlineData(1, 75, 1)]
    [InlineData(0, 75, 0)]
    [InlineData(5, 100, 5)]
    [InlineData(3, 50, 2)]
    public void MinimumAttendanceUsesCeiling(int total, int threshold, int expected) =>
        Assert.Equal(expected, AttendanceRules.MinLecturesRequired(total, threshold));

    [Fact]
    public void ZeroLecturesNeverQualifies() =>
        Assert.False(AttendanceRules.IsSatisfied(0, 0, 75));

    [Fact]
    public void AttendanceThresholdIsAppliedAtBoundary()
    {
        Assert.False(AttendanceRules.IsSatisfied(7, 10, 75));
        Assert.True(AttendanceRules.IsSatisfied(8, 10, 75));
    }

    [Theory]
    [InlineData(CourseStatus.Proposed, CourseStatus.Approved, true)]
    [InlineData(CourseStatus.Proposed, CourseStatus.Returned, true)]
    [InlineData(CourseStatus.Proposed, CourseStatus.Rejected, true)]
    [InlineData(CourseStatus.Returned, CourseStatus.Proposed, true)]
    [InlineData(CourseStatus.Approved, CourseStatus.Finished, true)]
    [InlineData(CourseStatus.Approved, CourseStatus.Cancelled, true)]
    [InlineData(CourseStatus.Finished, CourseStatus.Approved, true)]
    [InlineData(CourseStatus.Finished, CourseStatus.Cancelled, false)]
    [InlineData(CourseStatus.Rejected, CourseStatus.Approved, false)]
    public void CourseTransitionsFollowPack(CourseStatus from, CourseStatus to, bool allowed) =>
        Assert.Equal(allowed, CourseStateMachine.CanTransition(from, to));

    [Fact]
    public void ExamAndAttendanceOpenSurveyAfterFinish()
    {
        var input = new EligibilityInput(CourseStatus.Finished, true, 10, 8, 75,
            ExamState.Passed, false, true, true);
        var result = EligibilityRules.Evaluate(input);
        Assert.True(result.SurveyOpen);
        Assert.True(result.ImpactOpen);
        Assert.False(result.Eligible);
        Assert.Contains("EVALUATION_MISSING", result.UnmetReasons);
        Assert.DoesNotContain("IMPACT_NOT_APPROVED", result.UnmetReasons);
    }

    [Fact]
    public void CancelledCourseCannotIssueCertificate()
    {
        var input = new EligibilityInput(CourseStatus.Cancelled, false, 10, 10, 75,
            ExamState.None, true, true, true);
        Assert.False(EligibilityRules.Evaluate(input).Eligible);
    }

    [Fact]
    public void EligibilityRequiresMandatoryConditionsOnly()
    {
        var valid = new EligibilityInput(CourseStatus.Finished, true, 10, 8, 75,
            ExamState.Passed, true, true, true);
        Assert.True(EligibilityRules.Evaluate(valid).Eligible);
        Assert.False(EligibilityRules.Evaluate(valid with { Attended = 7 }).Eligible);
        Assert.False(EligibilityRules.Evaluate(valid with { Exam = ExamState.NotRecorded }).Eligible);
        Assert.False(EligibilityRules.Evaluate(valid with { Exam = ExamState.Failed }).Eligible);
        Assert.False(EligibilityRules.Evaluate(valid with { SurveyCompleted = false }).Eligible);
        Assert.False(EligibilityRules.Evaluate(valid with { ParticipantActive = false }).Eligible);
        Assert.False(EligibilityRules.Evaluate(valid with { CourseActive = false }).Eligible);
        Assert.False(EligibilityRules.Evaluate(valid with { Status = CourseStatus.Approved }).Eligible);
    }

    [Fact]
    public void RulesNormalizeNamesAndCertificateNumbers()
    {
        Assert.Equal("4069", LoginNameRules.NormalizeLogin(" ٤٠٦٩ "));
        Assert.True(LoginNameRules.IsExternalLogin("8123"));
        Assert.Equal("محمد علي حسن", NameRules.Join(" محمد  ", null, " علي", "حسن "));
        Assert.Equal("ZU-2026-IT-0007", CertificateReference.Format("ZU-{YYYY}-{CODE}-{NNNN}", 2026, "IT", 7, 4));
    }

    [Fact]
    public void ArabicSearchNormalizesHamzaAndTashkeel()
    {
        Assert.Equal("احمد", ArabicSearchNormalizer.Normalize("أحْمَد"));
        Assert.Equal("مدرسه", ArabicSearchNormalizer.Normalize("مدرسة"));
    }

    [Fact]
    public void PasswordPolicyAcceptsArabicAndRejectsContext()
    {
        var policy = new PasswordPolicy { MinLength = 15, MaxLength = 128, AllowArabic = true, AllowSpaces = true,
            BlockCommon = true, BlockContext = true };
        var candidate = "عبارة عربية طويلة جدا";
        Assert.Empty(PasswordPolicyRules.Validate(candidate, policy, [], []));
        Assert.Contains("PWD_CONTAINS_CONTEXT", PasswordPolicyRules.Validate(candidate, policy, [], ["عربية"]));
        Assert.Contains("PWD_BLOCKED", PasswordPolicyRules.Validate(candidate, policy,
            [candidate.ToLowerInvariant()], []));
    }

    [Fact]
    public void FileRuleChecksSignatureAsWellAsExtension()
    {
        Assert.True(FileRules.IsAllowed("a.pdf", "%PDF-1.7"u8.ToArray(), ["pdf"]));
        Assert.False(FileRules.IsAllowed("a.pdf", "not a pdf"u8.ToArray(), ["pdf"]));
        Assert.False(FileRules.IsAllowed("a.jpg", "%PDF-1.7"u8.ToArray(), ["jpg"]));
    }
}
