using System.IO.Compression;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Domain.Tests;

public sealed class PolicyAndFileTests
{
    private static PasswordPolicy Policy() => new()
    {
        MinLength = 15,
        MaxLength = 128,
        AllowSpaces = true,
        AllowArabic = true,
        BlockCommon = true,
        BlockContext = true
    };

    [Theory]
    [InlineData("short", "PWD_TOO_SHORT")]
    [InlineData("عضو تجريبي test1", "PWD_CONTAINS_CONTEXT")]
    public void RejectsLengthAndContext(string candidate, string code)
    {
        var errors = PasswordPolicyRules.Validate(candidate, Policy(), [], ["test1"]);
        Assert.Contains(code, errors);
    }

    [Fact]
    public void IgnoresInitialsAndOtherVeryShortContextTokens()
    {
        var errors = PasswordPolicyRules.Validate("Complete#Password9", Policy(), [], ["C", "A B", "Sample User"]);
        Assert.DoesNotContain("PWD_CONTAINS_CONTEXT", errors);
        Assert.Contains("PWD_CONTAINS_CONTEXT",
            PasswordPolicyRules.Validate("complete#password9", Policy(), [], ["Pass"]));
    }

    [Fact]
    public void RejectsConfiguredCharacterRules()
    {
        var policy = Policy();
        policy.AllowSpaces = false;
        policy.AllowArabic = false;
        policy.RequireUpper = true;
        policy.RequireLower = true;
        policy.RequireDigit = true;
        policy.RequireSymbol = true;
        var errors = PasswordPolicyRules.Validate("عبارة عربية طويلة جدا", policy, [], []);
        Assert.Contains("PWD_NO_SPACES", errors);
        Assert.Contains("PWD_NO_ARABIC", errors);
        Assert.Contains("PWD_NEED_UPPER", errors);
        Assert.Contains("PWD_NEED_LOWER", errors);
        Assert.Contains("PWD_NEED_DIGIT", errors);
        Assert.Contains("PWD_NEED_SYMBOL", errors);
    }

    [Fact]
    public void RejectsLongAndCommonPasswords()
    {
        var policy = Policy();
        policy.MaxLength = 15;
        var errors = PasswordPolicyRules.Validate("SamplePasswordLonger", policy, ["samplepasswordlonger"], []);
        Assert.Contains("PWD_TOO_LONG", errors);
        Assert.Contains("PWD_BLOCKED", errors);
    }

    [Fact]
    public void FullCourseTransitionMatrixMatchesSpecification()
    {
        var allowed = new HashSet<(CourseStatus, CourseStatus)>
        {
            (CourseStatus.Proposed, CourseStatus.Approved),
            (CourseStatus.Proposed, CourseStatus.AwaitingAccount),
            (CourseStatus.Proposed, CourseStatus.Returned),
            (CourseStatus.Proposed, CourseStatus.Rejected),
            (CourseStatus.Returned, CourseStatus.Proposed),
            (CourseStatus.Approved, CourseStatus.Finished),
            (CourseStatus.Approved, CourseStatus.Cancelled),
            (CourseStatus.AwaitingAccount, CourseStatus.Approved),
            (CourseStatus.AwaitingAccount, CourseStatus.Returned),
            (CourseStatus.AwaitingAccount, CourseStatus.Rejected),
            (CourseStatus.AwaitingAccount, CourseStatus.Cancelled),
            (CourseStatus.Finished, CourseStatus.Approved)
        };
        foreach (var from in Enum.GetValues<CourseStatus>())
            foreach (var to in Enum.GetValues<CourseStatus>())
                Assert.Equal(allowed.Contains((from, to)), CourseStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void SelfRegistrationRequiresAnOpenMatchingCourse()
    {
        var now = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 11, 10);
        var course = new Course
        {
            Status = CourseStatus.Approved,
            IsActive = true,
            PlannedEndDate = today.AddDays(1),
            AudienceType = AudienceType.General
        };

        SelfRegistrationEligibility Evaluate(bool academic = true, long centerId = 10,
            int participants = 0) => SelfRegistrationRules.Evaluate(course, 7, centerId,
                academic, UniversityOrganizationRules.ActiveEmployeeStatus, now, today,
                participants);

        Assert.Equal(SelfRegistrationEligibility.Eligible, Evaluate());

        course.AudienceType = AudienceType.Faculty;
        course.TargetCenterId = 10;
        Assert.Equal(SelfRegistrationEligibility.Eligible, Evaluate(centerId: 10));
        Assert.Equal(SelfRegistrationEligibility.NotEligible, Evaluate(centerId: 11));

        course.AudienceType = AudienceType.Administrative;
        Assert.Equal(SelfRegistrationEligibility.Eligible, Evaluate(academic: false));
        Assert.Equal(SelfRegistrationEligibility.NotEligible, Evaluate(academic: true));

        course.AudienceType = AudienceType.Named;
        Assert.Equal(SelfRegistrationEligibility.NotEligible, Evaluate());

        course.AudienceType = AudienceType.General;
        course.RegistrationDeadline = now.AddSeconds(-1);
        Assert.Equal(SelfRegistrationEligibility.Closed, Evaluate());
        course.RegistrationDeadline = null;

        course.PlannedEndDate = today.AddDays(-1);
        Assert.Equal(SelfRegistrationEligibility.NotEligible, Evaluate());
        course.PlannedEndDate = today.AddDays(1);

        course.InstructorUserId = 7;
        Assert.Equal(SelfRegistrationEligibility.Instructor, Evaluate());
        course.InstructorUserId = null;

        course.MaxParticipants = 1;
        Assert.Equal(SelfRegistrationEligibility.Full, Evaluate(participants: 1));
    }

    [Fact]
    public void OfficeArchiveMustContainExpectedDirectory()
    {
        byte[] docx = MakeOfficeArchive("word/document.xml");
        Assert.True(FileRules.IsAllowed("file.docx", docx, ["docx"]));
        Assert.False(FileRules.IsAllowed("file.pptx", docx, ["pptx"]));
        Assert.False(FileRules.IsAllowed("file.docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 }, ["docx"]));
    }

    private static byte[] MakeOfficeArchive(string part)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var contentTypes = archive.CreateEntry("[Content_Types].xml").Open())
                contentTypes.Write("<Types/>"u8);
            using (var document = archive.CreateEntry(part).Open())
                document.Write("<document/>"u8);
        }
        return buffer.ToArray();
    }
}
