using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Sync;
using ZuLms.Application.Features.Courses;
using ZuLms.Application.Features.Permissions;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Entities.Org;
using ZuLms.Domain.Entities.Sync;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Infrastructure.Persistence;
using ZuLms.Infrastructure.Time;

namespace ZuLms.Application.Tests;

public sealed class OrganizationMergePostgresTests
{
    [OrganizationPostgresFact]
    public async Task PreservesSideScopedIdentityAndAdministrativeEmployeesWhileEnrichingPrograms()
    {
        string connection = Environment.GetEnvironmentVariable("ZU_ORG_TEST_PG")!;
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        Assert.Contains("_orgtest_", builder.Database, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        db.UniversitySides.AddRange(
            new UniversitySide { SideCode = 320, NameAr = "Fixture university", IsImported = true },
            new UniversitySide { SideCode = 321, NameAr = "Fixture school", IsImported = true });
        var run = new SyncRun { StartedAt = DateTimeOffset.UtcNow, Status = SyncStatus.Partial, StatsJson = "{}" };
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync();
        var settings = new Settings();
        var merge = new UniversityMergeService(db, new SystemClock(), settings, new UserState());
        await merge.ApplyCentersAsync([
            new(320, 1, "University HR", 2, null, null),
            new(320, 2, "University finance", 2, null, null),
            new(321, 1, "School administration", 2, null, null)
        ], run.Id, CancellationToken.None);
        await merge.ApplyDepartmentsAsync([
            new(320, 10, "University office", 1, "0101", null),
            new(320, 20, "Finance office", 2, null, null),
            new(321, 10, "School office", 1, "0101", null)
        ], run.Id, CancellationToken.None);
        var source = new[]
        {
            Employee(320, 1, 10), Employee(320, 2, 20), Employee(321, 1, 10)
        };
        await merge.ApplyEmployeesAsync(source, run.Id, CancellationToken.None);
        var employees = await db.Employees.OrderBy(x => x.SideCode).ThenBy(x => x.EmployeeCode).ToListAsync();
        Assert.Equal(3, employees.Count);
        Assert.All(employees, employee => Assert.False(employee.IsAcademic));
        var university = employees.Single(x => x.SideCode == 320 && x.EmployeeCode == 1);
        var school = employees.Single(x => x.SideCode == 321);
        Assert.NotEqual(university.Id, school.Id);
        Assert.NotEqual(university.CenterId, school.CenterId);
        Assert.NotEqual(university.DepartmentId, school.DepartmentId);
        var inconsistent = employees.Single(x => x.SideCode == 320 && x.EmployeeCode == 2);
        Assert.Null(inconsistent.DepartmentId);
        Assert.Equal(20, inconsistent.DepartmentCode); // Raw HR code is never discarded.
        Assert.True(await db.SyncReviewItems.AnyAsync(x => x.EntityKey == "320:2" &&
            x.Reason == SyncReviewReason.UnresolvedReference && x.ResolvedAt == null));
        Assert.Equal(0, await db.Users.CountAsync()); // No provisioning or credentials are involved.

        await merge.ApplyProgramsAsync([new("0101", "01", "Academic English name", false)], run.Id, CancellationToken.None);
        var departments = await db.Departments.ToListAsync();
        Assert.Equal("Academic English name", departments.Single(x => x.SideCode == 320 && x.DepartmentCode == 10).NameEn);
        Assert.Null(departments.Single(x => x.SideCode == 321).NameEn);
        Assert.Null(departments.Single(x => x.DepartmentCode == 20).NameEn);
        Assert.All(departments, department => Assert.True(department.IsActive));
        Assert.All(await db.Centers.ToListAsync(), center => Assert.False(center.IsFaculty));

        // Simulate a legacy wrong cross-side FK and verify scoped reconciliation repairs it.
        university.CenterId = school.CenterId;
        university.DepartmentId = school.DepartmentId;
        await db.SaveChangesAsync();
        Assert.True(await merge.ReconcileEmployeeOrganizationAsync(run.Id, CancellationToken.None) > 0);
        Assert.NotEqual(university.CenterId, school.CenterId);
        Assert.NotEqual(university.DepartmentId, school.DepartmentId);
        Assert.Equal("على رأس عمله", university.EmpStatusDesc);
        Assert.Equal(0, await merge.ReconcileEmployeeOrganizationAsync(run.Id, CancellationToken.None));

        // An administrative/secretarial employee can see and join a general course.
        // IsAcademic and the existence of an academic program are not prerequisites.
        Assert.False(university.IsAcademic);
        var member = new User
        {
            AccountType = AccountType.Employee,
            EmployeeId = university.Id,
            LoginName = "100001",
            FullNameAr = "Fixture secretary",
            IsActive = true,
            SecurityStamp = Guid.NewGuid(),
            PreferredLanguage = UiLanguage.Ar,
            CredentialsEmailStatus = CredentialsEmailStatus.NotSent
        };
        var track = new TrainingTrack { NameAr = "Administrative training", IsActive = true };
        db.Users.Add(member); db.TrainingTracks.Add(track);
        await db.SaveChangesAsync();
        var course = new Course
        {
            CourseNo = "ORG-ADMIN-TEST",
            TitleAr = "Office skills",
            TrackId = track.Id,
            DeliveryMode = DeliveryMode.InPerson,
            TotalHours = 2,
            PlannedStartDate = new DateOnly(2026, 11, 1),
            PlannedEndDate = new DateOnly(2026, 11, 1),
            Origin = CourseOrigin.AdminCreated,
            AudienceType = AudienceType.General,
            Status = CourseStatus.Approved,
            StatusChangedAt = DateTimeOffset.UtcNow,
            InstructorKind = InstructorKind.ExternalNameOnly,
            InstructorNameAr = "Fixture trainer",
            ManagedByQuality = true,
            IsActive = true
        };
        db.Courses.Add(course); await db.SaveChangesAsync();
        var registration = new SelfRegistrationService(db, new SystemClock(), new Permissions(), settings, new Audit());
        var available = await registration.ListAvailableAsync(member.Id, 1, CancellationToken.None);
        Assert.True(available.Ok);
        Assert.Contains(available.Value!.Items, item => item.Id == course.Id);
        Assert.True((await registration.RegisterAsync(member.Id, course.Id, CancellationToken.None)).Ok);
        Assert.True(await db.CourseParticipants.AnyAsync(x => x.CourseId == course.Id && x.UserId == member.Id && x.IsActive));

        // A fresh request must see newly derived instructor access immediately,
        // even when the previous request cached this member without that role.
        var instructorRole = new Role { Code = RoleCode.Instructor, NameAr = "Fixture instructor", NameEn = "Fixture instructor" };
        var rosterPermission = new Permission
        {
            PermKey = "attendance.edit_manual",
            GroupKey = "attendance",
            NameAr = "Fixture roster",
            NameEn = "Fixture roster",
            Kind = PermissionKind.Action,
            Scope = PermissionScope.Global,
            IsActive = true
        };
        db.Roles.Add(instructorRole); db.Permissions.Add(rosterPermission); await db.SaveChangesAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = instructorRole.Id, PermissionId = rosterPermission.Id });
        await db.SaveChangesAsync();
        var beforeAssignment = new PermissionService(db, new TestActiveMode());
        Assert.False(await beforeAssignment.HasAsync(member.Id, "attendance.edit_manual", CancellationToken.None));
        course.InstructorUserId = member.Id; course.InstructorKind = InstructorKind.Internal; course.ManagedByQuality = false;
        await db.SaveChangesAsync();
        var nextRequest = new PermissionService(db, new TestActiveMode());
        Assert.True(await nextRequest.HasAsync(member.Id, "attendance.edit_manual", CancellationToken.None));
        var lecture = new Lecture
        {
            CourseId = course.Id,
            Mode = LectureMode.InPerson,
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddMinutes(30),
            AttendanceCode = "0000",
            LocationAr = "Fixture room",
            IsActive = true
        };
        db.Lectures.Add(lecture); await db.SaveChangesAsync();
        var attendance = new ManualAttendanceService(db, new SystemClock(), nextRequest, settings, null!, new Audit());
        Assert.True((await attendance.RosterAsync(member.Id, lecture.Id, CancellationToken.None)).Ok);
        course.InstructorUserId = null; course.InstructorKind = InstructorKind.ExternalNameOnly; course.ManagedByQuality = true;
        await db.SaveChangesAsync();
        Assert.False(await new PermissionService(db, new TestActiveMode()).HasAsync(member.Id, "attendance.edit_manual", CancellationToken.None));

        // Changing classification settings must take effect even if source hashes are unchanged.
        settings.AcademicCodes = "1,2";
        await merge.ApplyEmployeesAsync(source, run.Id, CancellationToken.None);
        Assert.All(employees, employee => Assert.True(employee.IsAcademic));
        await Assert.ThrowsAsync<InvalidDataException>(() => merge.ApplyProgramsAsync([
            new("0101", "01", "First", true), new("0101", "01", "Duplicate", true)
        ], run.Id, CancellationToken.None));
        await transaction.RollbackAsync();
    }

    private static EmployeeSource Employee(int side, int employee, int department)
    {
        using var json = JsonDocument.Parse($$"""
            {"side":{{side}},"employee_id":{{employee}},"fname":"Fixture secretary",
             "center_id":1,"department_id":{{department}},"main_job_code":2,"emp_status":13}
            """);
        return UniversitySourceMapper.Employee(json.RootElement);
    }

    private sealed class UserState : IUserStateCache { public void Invalidate(long userId) { } }
    private sealed class Audit : IAuditLogger
    {
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class Permissions : IPermissionService
    {
        public Task<bool> HasAsync(long userId, string key, CancellationToken cancellationToken) =>
            Task.FromResult(key == "participants.self_register");
        public Task<EffectivePermissionSet> GetEffectiveAsync(long userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AccountPermissionSet> GetAccountAsync(long userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Invalidate(long userId) { }
    }
    private sealed class TestActiveMode : IActiveModeAccessor { public RoleCode? SelectedMode => null; }
    private sealed class Settings : ISettingsService
    {
        public string AcademicCodes { get; set; } = "1";
        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            object value = key == "ui.default_page_size" ? 20 : key == "org.faculty_unit_codes" ? "8" : AcademicCodes;
            return Task.FromResult((T)value);
        }
        public Task<Result<IReadOnlyList<SystemSettingDto>>> ListAsync(long actorUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> SetAsync(string key, string value, uint expectedVersion, long actorUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Invalidate(string key) { }
    }
}

public sealed class OrganizationPostgresFactAttribute : FactAttribute
{
    public OrganizationPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZU_ORG_TEST_PG")))
            Skip = "Requires a dedicated _orgtest_ database through ZU_ORG_TEST_PG; never use the application database.";
    }
}
