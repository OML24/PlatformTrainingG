using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class AttendanceCodeService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit)
    : IAttendanceCodeService
{
    public async Task<Result> EnterAsync(long actorId, long lectureId,
        string code, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "attendance.enter_code", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var lecture = await db.Lectures.AsNoTracking().Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
            if (lecture?.Course is null || lecture.Course.Status != CourseStatus.Approved)
                return Result.Failure("ATT_NOT_PARTICIPANT");
            var participant = await db.CourseParticipants.SingleOrDefaultAsync(x =>
                x.CourseId == lecture.CourseId && x.UserId == actorId && x.IsActive, ct);
            if (participant is null) return Result.Failure("ATT_NOT_PARTICIPANT");
            DateTimeOffset now = clock.UtcNow;
            var attempt = await db.AttendanceCodeAttempts.SingleOrDefaultAsync(x =>
                x.LectureId == lectureId && x.UserId == actorId, ct);
            if (attempt?.LockedUntil > now) return Result.Failure("ATT_LOCKED");
            if (lecture.CodeActivatedAt is null || lecture.CodeExpiresAt <= now)
                return Result.Failure("ATT_CODE_EXPIRED");
            byte[] expected = Encoding.ASCII.GetBytes(lecture.AttendanceCode);
            byte[] supplied = Encoding.ASCII.GetBytes(code ?? "");
            bool equal = supplied.Length == expected.Length &&
                CryptographicOperations.FixedTimeEquals(supplied, expected);
            if (!equal)
            {
                int limit = await settings.GetAsync<int>("attendance.max_wrong_attempts", ct);
                int lockMinutes = await settings.GetAsync<int>("attendance.lock_minutes", ct);
                if (limit <= 0 || lockMinutes <= 0)
                    return Result.Failure("ATT_CODE_CONFIG_INVALID");
                attempt ??= new AttendanceCodeAttempt
                {
                    LectureId = lectureId, UserId = actorId
                };
                if (attempt.Id == 0) db.AttendanceCodeAttempts.Add(attempt);
                attempt.WrongCount++;
                attempt.LastAttemptAt = now;
                if (attempt.WrongCount >= limit)
                {
                    attempt.WrongCount = 0;
                    attempt.LockedUntil = now.AddMinutes(lockMinutes);
                }
                await db.SaveChangesAsync(ct);
                return Result.Failure("ATT_CODE_WRONG");
            }
            var record = await db.AttendanceRecords.SingleOrDefaultAsync(x =>
                x.LectureId == lectureId && x.ParticipantId == participant.Id, ct);
            bool? previous = record?.IsPresent;
            AttendanceSource? previousSource = record?.Source;
            if (record is null)
            {
                record = new AttendanceRecord
                {
                    LectureId = lectureId, ParticipantId = participant.Id
                };
                db.AttendanceRecords.Add(record);
            }
            record.IsPresent = true;
            record.Source = AttendanceSource.Code;
            record.MarkedAt = now;
            record.MarkedBy = actorId;
            db.AttendanceChanges.Add(new AttendanceChange
            {
                LectureId = lectureId, ParticipantId = participant.Id,
                OldPresent = previous, NewPresent = true,
                Source = AttendanceSource.Code, ChangedBy = actorId,
                ChangedAt = now
            });
            if (attempt is not null)
            {
                attempt.WrongCount = 0; attempt.LockedUntil = null;
                attempt.LastAttemptAt = now;
            }
            string memberName = await db.Users.AsNoTracking()
                .Where(x => x.Id == actorId).Select(x => x.FullNameAr)
                .SingleOrDefaultAsync(ct) ?? string.Empty;
            string lectureLabel = lecture.TitleAr ?? lecture.Course.TitleAr;
            // The member scanned/entered the code after staff had marked them absent manually.
            bool overridesManualAbsence = previous == false &&
                previousSource == AttendanceSource.Manual;
            await audit.LogAsync(new AuditEntry(AuditCategory.Attendance,
                overridesManualAbsence ? "attendance.code_after_manual_absent" : "attendance.enter_code",
                overridesManualAbsence
                    ? $"سجّل {memberName} حضوره بالباركود/الكود رغم تسجيله غائبًا يدويًا — {lectureLabel} ({lecture.Course.TitleAr})"
                    : $"سجّل {memberName} حضوره بالباركود/الكود — {lectureLabel} ({lecture.Course.TitleAr})",
                overridesManualAbsence
                    ? "Attendance code entered after manual absence"
                    : "Attendance code entered", "Lecture", lectureId,
                lectureLabel, CourseId: lecture.CourseId,
                AfterJson: JsonSerializer.Serialize(new
                {
                    participantId = participant.Id, memberName, previous,
                    previousSource = previousSource?.ToString(), isPresent = true,
                    source = "Code"
                }),
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}
