using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class ManualAttendanceService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings,
    IEligibilityRecalculationService recalculation, IAuditLogger audit)
    : IManualAttendanceService
{
    public async Task<Result<IReadOnlyList<AttendanceRosterRowDto>>> RosterAsync(
        long actorId, long lectureId, CancellationToken ct)
    {
        var lecture = await db.Lectures.AsNoTracking().Include(x => x.Course)
            .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
        if (lecture?.Course is null)
            return Result<IReadOnlyList<AttendanceRosterRowDto>>.Failure("LECTURE_NOT_FOUND");
        if (!await CanEditAsync(actorId, lecture.Course, ct))
            return Result<IReadOnlyList<AttendanceRosterRowDto>>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorId, ct)).HasMode(RoleCode.Admin);
        var rows = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == lecture.CourseId && x.IsActive &&
                (admin || x.User!.Employee != null &&
                    x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
            .OrderBy(x => x.User!.FullNameAr)
            .Select(x => new AttendanceRosterRowDto(x.Id, x.UserId,
                x.User!.FullNameAr, x.User.LoginName,
                db.AttendanceRecords.Any(a => a.LectureId == lectureId &&
                    a.ParticipantId == x.Id && a.IsPresent)))
            .ToListAsync(ct);
        return Result<IReadOnlyList<AttendanceRosterRowDto>>.Success(rows);
    }

    public async Task<Result> SetAsync(long actorId, long lectureId,
        long participantId, bool isPresent, string? reason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "attendance.edit_manual", ct))
            return Result.Failure("FORBIDDEN");
        if (reason?.Trim().Length > 1000)
            return Result.Failure("REASON_TOO_LONG");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var lecture = await db.Lectures.Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
            if (lecture?.Course is null) return Result.Failure("LECTURE_NOT_FOUND");
            if (!await CanEditAsync(actorId, lecture.Course, ct))
                return Result.Failure("FORBIDDEN");
            if (lecture.Course.Status == CourseStatus.Finished &&
                !await settings.GetAsync<bool>("attendance.allow_edit_after_finish", ct))
                return Result.Failure("ATT_EDIT_CLOSED");
            if (lecture.Course.Status is not (CourseStatus.Approved or CourseStatus.Finished))
                return Result.Failure("ATT_EDIT_CLOSED");
            var participant = await db.CourseParticipants.SingleOrDefaultAsync(x =>
                x.Id == participantId && x.CourseId == lecture.CourseId && x.IsActive, ct);
            if (participant is null) return Result.Failure("PARTICIPANT_NOT_FOUND");
            var record = await db.AttendanceRecords.SingleOrDefaultAsync(x =>
                x.LectureId == lectureId && x.ParticipantId == participantId, ct);
            bool? previous = record?.IsPresent;
            DateTimeOffset now = clock.UtcNow;
            if (record is null)
            {
                record = new AttendanceRecord
                {
                    LectureId = lectureId,
                    ParticipantId = participantId
                };
                db.AttendanceRecords.Add(record);
            }
            record.IsPresent = isPresent;
            record.Source = AttendanceSource.Manual;
            record.MarkedAt = now; record.MarkedBy = actorId;
            db.AttendanceChanges.Add(new AttendanceChange
            {
                LectureId = lectureId,
                ParticipantId = participantId,
                OldPresent = previous,
                NewPresent = isPresent,
                Source = AttendanceSource.Manual,
                ChangedBy = actorId,
                ChangedAt = now,
                Reason = Clean(reason)
            });
            await db.SaveChangesAsync(ct);
            var result = await recalculation.RecalculateParticipantAsync(
                lecture.CourseId, participantId,
                CertificateRevokeReason.AttendanceCorrection, actorId, ct);
            if (!result.Ok) return Result.Failure(result.Error!.Code);
            await audit.LogAsync(new AuditEntry(AuditCategory.Attendance,
                "attendance.edit_manual", "تعديل حضور يدوي",
                "Manual attendance edit", "Lecture", lectureId,
                lecture.TitleAr ?? lecture.Course.TitleAr,
                AfterJson: JsonSerializer.Serialize(new
                { participantId, previous, isPresent, reason }),
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<bool> CanEditAsync(long actorId, Course course,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "attendance.edit_manual", ct))
            return false;
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        return effective.HasMode(RoleCode.Admin) ||
            (effective.HasMode(RoleCode.Instructor) || effective.HasMode(RoleCode.ExternalInstructor)) && course.InstructorUserId == actorId ||
            course.ManagedByQuality && effective.HasMode(RoleCode.Quality);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
