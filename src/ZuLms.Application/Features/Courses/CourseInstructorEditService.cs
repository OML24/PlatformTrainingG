using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public enum CourseInstructorChoice
{
    Internal, ExistingExternal, ExternalNameOnly, ManagedByQuality
}

public sealed record CourseInstructorEditInput(uint ExpectedVersion,
    CourseInstructorChoice Choice, long? UserId, string? NameAr,
    string? NameEn, string? OrganizationAr, string? OrganizationEn);

public interface ICourseInstructorEditService
{
    Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchAsync(long actorUserId,
        long courseId, bool external, string? search, CancellationToken ct);
    Task<Result> EditAsync(long actorUserId, long courseId,
        CourseInstructorEditInput input, CancellationToken ct);
}

public sealed class CourseInstructorEditService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IAuditLogger audit) : ICourseInstructorEditService
{
    public async Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchAsync(
        long actorUserId, long courseId, bool external, string? search,
        CancellationToken ct)
    {
        var access = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!access.Keys.Contains("course.edit") || !access.HasMode(RoleCode.Admin))
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("FORBIDDEN");
        if (search?.Length > 100)
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("QUERY_INVALID");
        var managedByQuality = await db.Courses.IgnoreQueryFilters().Where(x => x.Id == courseId)
            .Select(x => (bool?)x.ManagedByQuality).SingleOrDefaultAsync(ct);
        if (managedByQuality is null)
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("COURSE_NOT_FOUND");
        if (managedByQuality is { } managed && !ScopeGuard.AllowsQualityManagedCourse(access, managed))
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("FORBIDDEN");
        bool admin = access.HasMode(RoleCode.Admin);
        string term = ArabicSearchNormalizer.Normalize(search);
        var query = db.Users.AsNoTracking().Where(x => x.IsActive &&
            x.AccountType == (external ? AccountType.ExternalInstructor : AccountType.Employee) &&
            (external || admin || x.Employee != null &&
                x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus));
        if (term.Length > 0)
            query = query.Where(x => x.SearchText.Contains(term) || x.LoginName.Contains(term));
        IReadOnlyList<InstructorOptionDto> rows = await query
            .OrderBy(x => x.FullNameAr).ThenBy(x => x.Id).Take(30)
            .Select(x => new InstructorOptionDto(x.Id, x.FullNameAr, x.LoginName,
                null, null, admin && x.Employee != null ? x.Employee.EmpStatus : null,
                admin && x.Employee != null ? x.Employee.EmpStatusDesc : null))
            .ToListAsync(ct);
        return Result<IReadOnlyList<InstructorOptionDto>>.Success(rows);
    }

    public async Task<Result> EditAsync(long actorUserId, long courseId,
        CourseInstructorEditInput input, CancellationToken ct)
    {
        var access = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool admin = access.HasMode(RoleCode.Admin);
        if (!access.Keys.Contains("course.edit") || !admin)
            return Result.Failure("FORBIDDEN");
        if (!Enum.IsDefined(input.Choice)) return Result.Failure("INSTRUCTOR_INVALID");
        bool account = input.Choice is CourseInstructorChoice.Internal or
            CourseInstructorChoice.ExistingExternal;
        if (account && (input.UserId is null || input.NameAr is not null ||
            input.NameEn is not null || input.OrganizationAr is not null ||
            input.OrganizationEn is not null) ||
            !account && input.UserId is not null ||
            input.Choice == CourseInstructorChoice.ExternalNameOnly &&
                (string.IsNullOrWhiteSpace(input.NameAr) ||
                 string.IsNullOrWhiteSpace(input.OrganizationAr)) ||
            input.Choice == CourseInstructorChoice.ManagedByQuality &&
                (input.NameAr is not null || input.NameEn is not null ||
                 input.OrganizationAr is not null || input.OrganizationEn is not null) ||
            input.NameAr?.Trim().Length > 300 || input.NameEn?.Trim().Length > 300 ||
            input.OrganizationAr?.Trim().Length > 300 ||
            input.OrganizationEn?.Trim().Length > 300)
            return Result.Failure("INSTRUCTOR_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (!ScopeGuard.AllowsQualityManagedCourse(access, course.ManagedByQuality)) return Result.Failure("FORBIDDEN");
            if (course.Version != input.ExpectedVersion) return Result.Failure("CONFLICT");
            if (!admin && course.Status == CourseStatus.Finished)
                return Result.Failure("INSTRUCTOR_EDIT_FINISHED");
            if (await db.ExternalInstructorAccountRequests.AnyAsync(x =>
                x.CourseId == courseId &&
                x.Status == ExternalAccountRequestStatus.PendingAdmin, ct))
                return Result.Failure("INSTRUCTOR_REQUEST_PENDING");
            var before = new
            {
                course.InstructorKind,
                course.InstructorUserId,
                course.InstructorNameAr,
                course.InstructorNameEn,
                course.InstructorOrgAr,
                course.InstructorOrgEn,
                course.ManagedByQuality
            };
            long? previousUserId = course.InstructorUserId;
            if (account)
            {
                AccountType required = input.Choice == CourseInstructorChoice.Internal
                    ? AccountType.Employee : AccountType.ExternalInstructor;
                var user = await db.Users.AsNoTracking().Where(x =>
                    x.Id == input.UserId && x.IsActive && x.AccountType == required &&
                    (required != AccountType.Employee || admin || x.Employee != null &&
                        x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
                    .Select(x => new { x.Id, x.FullNameAr, x.FullNameEn })
                    .SingleOrDefaultAsync(ct);
                if (user is null) return Result.Failure("INSTRUCTOR_INVALID");
                course.InstructorKind = input.Choice == CourseInstructorChoice.Internal
                    ? InstructorKind.Internal : InstructorKind.ExternalAccount;
                course.InstructorUserId = user.Id;
                course.InstructorNameAr = user.FullNameAr;
                course.InstructorNameEn = user.FullNameEn;
                var profile = input.Choice == CourseInstructorChoice.ExistingExternal
                    ? await db.ExternalInstructorProfiles.AsNoTracking()
                        .Where(x => x.UserId == user.Id)
                        .Select(x => new { x.OrganizationAr, x.OrganizationEn })
                        .SingleOrDefaultAsync(ct) : null;
                course.InstructorOrgAr = profile?.OrganizationAr;
                course.InstructorOrgEn = profile?.OrganizationEn;
                course.ManagedByQuality = false;
            }
            else
            {
                course.InstructorKind = input.Choice == CourseInstructorChoice.ManagedByQuality
                    ? InstructorKind.ManagedByQuality : InstructorKind.ExternalNameOnly;
                course.InstructorUserId = null;
                course.InstructorNameAr = input.Choice == CourseInstructorChoice.ManagedByQuality
                    ? "تديرها الجودة" : input.NameAr!.Trim();
                course.InstructorNameEn = Clean(input.NameEn);
                course.InstructorOrgAr = Clean(input.OrganizationAr);
                course.InstructorOrgEn = Clean(input.OrganizationEn);
                course.ManagedByQuality = true;
            }
            course.SearchText = ArabicSearchNormalizer.Normalize(
                $"{course.CourseNo} {course.TitleAr} {course.TitleEn} {course.InstructorNameAr}");
            course.UpdatedAt = clock.UtcNow;
            course.UpdatedByUserId = actorUserId;
            if (course.InstructorUserId is long newUserId && newUserId != previousUserId)
                db.Notifications.Add(new Notification
                {
                    UserId = newUserId,
                    Type = NotificationType.CourseApproved,
                    TitleAr = "تكليف بمحاضرة دورة",
                    TitleEn = "Course instructor assigned",
                    BodyAr = course.TitleAr,
                    BodyEn = course.TitleEn,
                    LinkUrl = $"/courses/{course.Id}",
                    RelatedEntityType = "Course",
                    RelatedEntityId = course.Id,
                    CreatedAt = clock.UtcNow,
                    IsActive = true
                });
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "course.instructor.edit", "تعديل محاضر الدورة", "Course instructor edited",
                "Course", courseId, course.TitleAr, courseId,
                BeforeJson: JsonSerializer.Serialize(before),
                AfterJson: JsonSerializer.Serialize(new
                {
                    course.InstructorKind,
                    course.InstructorUserId,
                    course.InstructorNameAr,
                    course.InstructorNameEn,
                    course.InstructorOrgAr,
                    course.InstructorOrgEn,
                    course.ManagedByQuality
                }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
