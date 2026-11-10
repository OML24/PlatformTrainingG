using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class ManualParticipantService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IAuditLogger audit) : IManualParticipantService
{
    public async Task<Result<ManualParticipantPageDto>> ListAsync(long actorUserId,
        long courseId, CancellationToken ct)
    {
        if (!await CanManageAsync(actorUserId, "participants.add", ct) &&
            !await CanManageAsync(actorUserId, "participants.remove", ct))
            return Result<ManualParticipantPageDto>.Failure("FORBIDDEN");
        var course = await db.Courses.AsNoTracking().Where(x => x.Id == courseId)
            .Select(x => new { x.Id, x.CourseNo, x.TitleAr, x.MaxParticipants, x.ManagedByQuality })
            .SingleOrDefaultAsync(ct);
        if (course is null)
            return Result<ManualParticipantPageDto>.Failure("COURSE_NOT_FOUND");
        if (!await CanManageCourseAsync(actorUserId, course.ManagedByQuality, ct))
            return Result<ManualParticipantPageDto>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        var participantRows = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive &&
                (admin || x.User!.Employee != null &&
                    x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
            .OrderBy(x => x.User!.FullNameAr).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.UserId,
                x.User!.FullNameAr,
                x.User.LoginName,
                EmpStatus = admin && x.User.Employee != null ? x.User.Employee.EmpStatus : null,
                EmpStatusDesc = admin && x.User.Employee != null ? x.User.Employee.EmpStatusDesc : null,
                x.Source,
                x.JoinedAt,
                HasIssuedCertificate =
                    db.Certificates.Any(c => c.ParticipantId == x.Id &&
                        c.Status == CertificateStatus.Issued)
            })
            .ToListAsync(ct);
        var rows = participantRows.Select(x => new ManualParticipantRowDto(x.UserId,
            x.FullNameAr, x.LoginName, x.Source.ToString(), x.JoinedAt,
            x.HasIssuedCertificate, x.EmpStatus, x.EmpStatusDesc)).ToList();
        return Result<ManualParticipantPageDto>.Success(new(course.Id,
            course.CourseNo, course.TitleAr, course.MaxParticipants, rows));
    }

    public async Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchEmployeesAsync(
        long actorUserId, string? search, CancellationToken ct)
    {
        if (!await CanManageAsync(actorUserId, "participants.add", ct))
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        string term = ArabicSearchNormalizer.Normalize(search);
        var query = db.Users.AsNoTracking().Where(x => x.IsActive &&
            x.AccountType == AccountType.Employee && x.Employee != null &&
            (admin || x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus));
        if (term.Length > 0)
            query = query.Where(x => x.SearchText.Contains(term) || x.LoginName.Contains(term));
        var rows = await query.OrderBy(x => x.FullNameAr).ThenBy(x => x.Id).Take(30)
            .Select(x => new InstructorOptionDto(x.Id, x.FullNameAr, x.LoginName,
                null, null, admin ? x.Employee!.EmpStatus : null,
                admin ? x.Employee!.EmpStatusDesc : null))
            .ToListAsync(ct);
        return Result<IReadOnlyList<InstructorOptionDto>>.Success(rows);
    }

    public async Task<Result> AddAsync(long actorUserId, long courseId,
        IReadOnlyList<long> userIds, bool confirmCapacityOverride, CancellationToken ct)
    {
        if (!await CanManageAsync(actorUserId, "participants.add", ct))
            return Result.Failure("FORBIDDEN");
        if (userIds.Count == 0 || userIds.Count > 100 ||
            userIds.Any(x => x <= 0) || userIds.Distinct().Count() != userIds.Count)
            return Result.Failure("PARTICIPANTS_INVALID");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (!await CanManageCourseAsync(actorUserId, course.ManagedByQuality, ct)) return Result.Failure("FORBIDDEN");
            if (course.IsHistorical) return Result.Failure("HISTORICAL_READ_ONLY");
            if (course.Status is CourseStatus.AwaitingAccount or CourseStatus.Cancelled ||
                course.Status == CourseStatus.Finished)
                return Result.Failure("PARTICIPANTS_CLOSED");
            var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id) &&
                x.IsActive && x.AccountType == AccountType.Employee && x.Employee != null &&
                (admin || x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
                .Select(x => x.Id).ToListAsync(ct);
            if (users.Count != userIds.Count || userIds.Contains(course.InstructorUserId ?? -1))
                return Result.Failure("PARTICIPANTS_INVALID");
            var existing = await db.CourseParticipants.Where(x => x.CourseId == courseId &&
                userIds.Contains(x.UserId)).ToListAsync(ct);
            if (existing.Any(x => x.IsActive)) return Result.Failure("REG_ALREADY");
            int active = await db.CourseParticipants.CountAsync(x => x.CourseId == courseId &&
                x.IsActive, ct);
            if (course.MaxParticipants is int max && active + userIds.Count > max &&
                !confirmCapacityOverride) return Result.Failure("CAPACITY_CONFIRM_REQUIRED");
            var roles = await permissions.GetEffectiveAsync(actorUserId, ct);
            ParticipantSource source = roles.HasMode(RoleCode.Admin)
                ? ParticipantSource.AddedByAdmin : ParticipantSource.AddedByQuality;
            DateTimeOffset now = clock.UtcNow;
            foreach (long userId in userIds)
            {
                var participant = existing.SingleOrDefault(x => x.UserId == userId);
                if (participant is null)
                    db.CourseParticipants.Add(new CourseParticipant
                    {
                        CourseId = courseId,
                        UserId = userId,
                        Source = source,
                        AddedByUserId = actorUserId,
                        JoinedAt = now,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedByUserId = actorUserId
                    });
                else
                {
                    participant.Source = source; participant.AddedByUserId = actorUserId;
                    participant.JoinedAt = now; participant.IsActive = true;
                    participant.RemovedAt = null; participant.RemovedBy = null;
                    participant.RemovalReason = null;
                    participant.UpdatedAt = now; participant.UpdatedByUserId = actorUserId;
                }
                db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.ParticipantAdded,
                    TitleAr = "إضافتك إلى دورة",
                    TitleEn = "Added to a course",
                    BodyAr = course.TitleAr,
                    BodyEn = course.TitleEn,
                    LinkUrl = $"/courses/{courseId}",
                    RelatedEntityType = "Course",
                    RelatedEntityId = courseId,
                    CreatedAt = now,
                    IsActive = true
                });
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Course, "participants.add",
                "إضافة مشاركين", "Participants added", "Course", courseId,
                course.TitleAr, AfterJson: System.Text.Json.JsonSerializer.Serialize(userIds),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> RemoveAsync(long actorUserId, long courseId,
        long userId, string reason, bool confirmCertificateRevocation,
        CancellationToken ct)
    {
        if (!await CanManageAsync(actorUserId, "participants.remove", ct))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return Result.Failure("REMOVAL_REASON_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (!await CanManageCourseAsync(actorUserId, course.ManagedByQuality, ct)) return Result.Failure("FORBIDDEN");
            if (course.Status == CourseStatus.AwaitingAccount) return Result.Failure("PARTICIPANTS_CLOSED");
            var participant = await db.CourseParticipants.SingleOrDefaultAsync(x =>
                x.CourseId == courseId && x.UserId == userId && x.IsActive, ct);
            if (participant is null) return Result.Failure("PARTICIPANT_NOT_FOUND");
            var certificates = await db.Certificates.Where(x => x.ParticipantId == participant.Id &&
                x.Status == CertificateStatus.Issued).ToListAsync(ct);
            if (certificates.Count > 0 && !confirmCertificateRevocation)
                return Result.Failure("CERTIFICATE_REVOCATION_CONFIRM_REQUIRED");
            DateTimeOffset now = clock.UtcNow;
            participant.IsActive = false; participant.RemovedAt = now;
            participant.RemovedBy = actorUserId; participant.RemovalReason = reason.Trim();
            participant.UpdatedAt = now; participant.UpdatedByUserId = actorUserId;
            foreach (var certificate in certificates)
            {
                certificate.Status = CertificateStatus.Revoked;
                certificate.RevokedAt = now;
                certificate.RevokedBy = actorUserId;
                certificate.RevokedReason = CertificateRevokeReason.ParticipantRemoved;
                certificate.RevokedNote = reason.Trim();
                certificate.IsActive = false;
            }
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = NotificationType.ParticipantRemoved,
                TitleAr = "إخراجك من دورة",
                TitleEn = "Removed from a course",
                BodyAr = course.TitleAr,
                BodyEn = course.TitleEn,
                LinkUrl = $"/courses/{courseId}",
                RelatedEntityType = "Course",
                RelatedEntityId = courseId,
                CreatedAt = now,
                IsActive = true
            });
            await audit.LogAsync(new AuditEntry(AuditCategory.Course, "participants.remove",
                "إخراج مشارك", "Participant removed", "Course", courseId,
                course.TitleAr, AfterJson: System.Text.Json.JsonSerializer.Serialize(new
                { userId, reason, revokedCertificates = certificates.Count }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<bool> CanManageAsync(long actorUserId, string key,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, key, ct)) return false;
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        return effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality);
    }

    private async Task<bool> CanManageCourseAsync(long actorUserId, bool managedByQuality, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        return ScopeGuard.AllowsQualityManagedCourse(effective, managedByQuality);
    }
}
