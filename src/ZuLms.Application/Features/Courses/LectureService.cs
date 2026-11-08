using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class LectureService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings,
    IAuditLogger audit) : ILectureService
{
    public async Task<Result<long>> CreateAsync(long actorId, long courseId,
        LectureInput input, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
        if (course is null) return Result<long>.Failure("COURSE_NOT_FOUND");
        if (course.IsHistorical) return Result<long>.Failure("HISTORICAL_READ_ONLY");
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        if (!effective.Keys.Contains("lecture.manage"))
            return Result<long>.Failure("FORBIDDEN");
        if (!await CanManageAsync(actorId, course, ct))
            return Result<long>.Failure("FORBIDDEN");
        if (course.Status == CourseStatus.Finished)
            return Result<long>.Failure("COURSE_FINISHED");
        if (course.Status != CourseStatus.Approved)
            return Result<long>.Failure("COURSE_NOT_APPROVED");
        string? error = Validate(course, input);
        if (error is not null) return Result<long>.Failure(error);
        int length = await settings.GetAsync<int>("attendance.code_length", ct);
        if (length is < 4 or > 8) return Result<long>.Failure("ATT_CODE_CONFIG_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            DateTimeOffset now = clock.UtcNow;
            var lecture = new Lecture
            {
                CourseId = courseId,
                TitleAr = Clean(input.TitleAr),
                TitleEn = Clean(input.TitleEn),
                Mode = input.Mode,
                StartsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                    input.StartsAt, clock.Amman), TimeSpan.Zero),
                EndsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                    input.EndsAt, clock.Amman), TimeSpan.Zero),
                LocationAr = input.Mode == LectureMode.InPerson
                    ? Clean(input.LocationAr) : null,
                LocationEn = input.Mode == LectureMode.InPerson
                    ? Clean(input.LocationEn) : null,
                TeamsUrl = input.Mode == LectureMode.Online
                    ? input.TeamsUrl!.Trim() : null,
                NotesAr = Clean(input.NotesAr),
                AttendanceCode = new string(Enumerable.Range(0, length)
                    .Select(_ => (char)('0' + RandomNumberGenerator.GetInt32(10))).ToArray()),
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = actorId
            };
            db.Lectures.Add(lecture);
            await db.SaveChangesAsync(ct);
            var participants = await db.CourseParticipants.AsNoTracking()
                .Where(x => x.CourseId == courseId && x.IsActive)
                .Select(x => x.UserId).ToListAsync(ct);
            foreach (long userId in participants)
                db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.LectureCreated,
                    TitleAr = "محاضرة جديدة",
                    TitleEn = "New lecture",
                    BodyAr = course.TitleAr,
                    BodyEn = course.TitleEn,
                    LinkUrl = $"/courses/{courseId}",
                    RelatedEntityType = "Lecture",
                    RelatedEntityId = lecture.Id,
                    CreatedAt = now,
                    IsActive = true
                });
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "lecture.create", "إنشاء محاضرة", "Lecture created",
                "Lecture", lecture.Id, course.TitleAr,
                AfterJson: JsonSerializer.Serialize(new
                {
                    courseId,
                    lecture.Mode,
                    lecture.StartsAt,
                    lecture.EndsAt
                }),
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(lecture.Id);
        }, ct);
    }

    public async Task<Result<IReadOnlyList<LectureDto>>> ListAsync(long actorId,
        long courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == courseId, ct);
        if (course is null)
            return Result<IReadOnlyList<LectureDto>>.Failure("COURSE_NOT_FOUND");
        bool manager = await CanManageAsync(actorId, course, ct);
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        bool participant = effective.HasMode(RoleCode.Member) &&
            effective.Keys.Contains("page.member.enrolled") &&
            await db.CourseParticipants.AnyAsync(x =>
                x.CourseId == courseId && x.UserId == actorId && x.IsActive, ct);
        if (!manager && !participant)
            return Result<IReadOnlyList<LectureDto>>.Failure("FORBIDDEN");
        var now = clock.UtcNow;
        var rows = await db.Lectures.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .Select(x => new LectureDto(x.Id, x.TitleAr, x.Mode,
                x.StartsAt, x.EndsAt, x.LocationAr, x.TeamsUrl,
                x.IsActive, x.CodeExpiresAt > now,
                db.AttendanceRecords.Any(a => a.LectureId == x.Id &&
                    a.Participant!.UserId == actorId && a.IsPresent)))
            .ToListAsync(ct);
        return Result<IReadOnlyList<LectureDto>>.Success(rows);
    }

    public async Task<Result<ActivatedAttendanceCodeDto>> ActivateCodeAsync(
        long actorId, long lectureId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "attendance.activate_code", ct))
            return Result<ActivatedAttendanceCodeDto>.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var lecture = await db.Lectures.Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
            if (lecture?.Course is null)
                return Result<ActivatedAttendanceCodeDto>.Failure("LECTURE_NOT_FOUND");
            if (lecture.Course.IsHistorical)
                return Result<ActivatedAttendanceCodeDto>.Failure("HISTORICAL_READ_ONLY");
            if (!await CanManageAsync(actorId, lecture.Course, ct))
                return Result<ActivatedAttendanceCodeDto>.Failure("FORBIDDEN");
            if (lecture.Course.Status != CourseStatus.Approved)
                return Result<ActivatedAttendanceCodeDto>.Failure("COURSE_NOT_APPROVED");
            DateTimeOffset now = clock.UtcNow;
            if (lecture.CodeExpiresAt > now)
                return Result<ActivatedAttendanceCodeDto>.Failure("ATT_CODE_ACTIVE");
            int minutes = await settings.GetAsync<int>(
                "attendance.code_validity_minutes", ct);
            if (minutes <= 0)
                return Result<ActivatedAttendanceCodeDto>.Failure("ATT_CODE_CONFIG_INVALID");
            lecture.CodeActivatedAt = now;
            lecture.CodeExpiresAt = now.AddMinutes(minutes);
            lecture.UpdatedAt = now; lecture.UpdatedByUserId = actorId;
            await audit.LogAsync(new AuditEntry(AuditCategory.Attendance,
                "attendance.activate_code", "تفعيل كود الحضور",
                "Attendance code activated", "Lecture", lecture.Id,
                lecture.TitleAr ?? lecture.Course.TitleAr,
                AfterJson: JsonSerializer.Serialize(new
                { lecture.CodeActivatedAt, lecture.CodeExpiresAt }),
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result<ActivatedAttendanceCodeDto>.Success(new(lecture.Id,
                lecture.AttendanceCode, lecture.CodeExpiresAt.Value));
        }, ct);
    }

    public async Task<Result> UpdateAsync(long actorId, long lectureId,
        LectureScheduleInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "lecture.manage", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var lecture = await db.Lectures.Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
            if (lecture?.Course is null) return Result.Failure("LECTURE_NOT_FOUND");
            if (lecture.Course.IsHistorical) return Result.Failure("HISTORICAL_READ_ONLY");
            if (!await CanManageAsync(actorId, lecture.Course, ct))
                return Result.Failure("FORBIDDEN");
            if (lecture.Course.Status == CourseStatus.Finished)
                return Result.Failure("COURSE_FINISHED");
            if (lecture.Course.Status != CourseStatus.Approved)
                return Result.Failure("COURSE_NOT_APPROVED");
            var validation = Validate(lecture.Course, new LectureInput(
                lecture.TitleAr, lecture.TitleEn, lecture.Mode,
                input.StartsAt, input.EndsAt,
                input.LocationAr, input.LocationEn, input.TeamsUrl,
                lecture.NotesAr));
            if (validation is not null) return Result.Failure(validation);
            var before = new
            {
                lecture.StartsAt,
                lecture.EndsAt,
                lecture.LocationAr,
                lecture.LocationEn,
                lecture.TeamsUrl
            };
            lecture.StartsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                input.StartsAt, clock.Amman), TimeSpan.Zero);
            lecture.EndsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                input.EndsAt, clock.Amman), TimeSpan.Zero);
            lecture.LocationAr = lecture.Mode == LectureMode.InPerson
                ? Clean(input.LocationAr) : null;
            lecture.LocationEn = lecture.Mode == LectureMode.InPerson
                ? Clean(input.LocationEn) : null;
            lecture.TeamsUrl = lecture.Mode == LectureMode.Online
                ? input.TeamsUrl!.Trim() : null;
            lecture.UpdatedAt = clock.UtcNow;
            lecture.UpdatedByUserId = actorId;
            await audit.LogAsync(new AuditEntry(AuditCategory.Lecture,
                "lecture.update", "تعديل موعد المحاضرة أو مكانها",
                "Lecture schedule or location changed", "Lecture", lectureId,
                lecture.TitleAr ?? lecture.Course.TitleAr,
                BeforeJson: JsonSerializer.Serialize(before),
                AfterJson: JsonSerializer.Serialize(new
                {
                    lecture.StartsAt,
                    lecture.EndsAt,
                    lecture.LocationAr,
                    lecture.LocationEn,
                    lecture.TeamsUrl
                }),
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<bool> CanManageAsync(long actorId, Course course,
        CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        return effective.Keys.Contains("lecture.manage") &&
            ScopeGuard.Allows(effective, PermissionScope.InstructedCourse,
            instructorUserId: course.InstructorUserId, managedByQuality: course.ManagedByQuality);
    }

    private static string? Validate(Course course, LectureInput input)
    {
        if (!Enum.IsDefined(input.Mode) || input.EndsAt <= input.StartsAt ||
            input.StartsAt == default ||
            input.StartsAt.Kind != DateTimeKind.Unspecified ||
            input.EndsAt.Kind != DateTimeKind.Unspecified ||
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman").IsInvalidTime(input.StartsAt) ||
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman").IsInvalidTime(input.EndsAt) ||
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman").IsAmbiguousTime(input.StartsAt) ||
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman").IsAmbiguousTime(input.EndsAt) ||
            input.TitleAr?.Trim().Length > 300 || input.TitleEn?.Trim().Length > 300 ||
            input.LocationAr?.Trim().Length > 300 ||
            input.LocationEn?.Trim().Length > 300 ||
            input.TeamsUrl?.Trim().Length > 600)
            return "LECTURE_INVALID";
        if (course.DeliveryMode == DeliveryMode.InPerson &&
            input.Mode != LectureMode.InPerson ||
            course.DeliveryMode == DeliveryMode.Online &&
            input.Mode != LectureMode.Online)
            return "LECTURE_MODE_INVALID";
        if (input.Mode == LectureMode.InPerson &&
            string.IsNullOrWhiteSpace(input.LocationAr))
            return "LECTURE_LOCATION_REQUIRED";
        if (input.Mode == LectureMode.Online &&
            (!Uri.TryCreate(input.TeamsUrl, UriKind.Absolute, out var url) ||
             url.Scheme != Uri.UriSchemeHttps))
            return "LECTURE_TEAMS_URL_INVALID";
        return null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
