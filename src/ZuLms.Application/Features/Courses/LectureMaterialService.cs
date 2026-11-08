using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class LectureMaterialService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IAuditLogger audit)
    : ILectureMaterialService
{
    public async Task<Result<IReadOnlyList<LectureMaterialDto>>> ListAsync(
        long actorId, long lectureId, CancellationToken ct)
    {
        var lecture = await db.Lectures.AsNoTracking().Include(x => x.Course)
            .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
        if (lecture?.Course is null)
            return Result<IReadOnlyList<LectureMaterialDto>>
                .Failure("LECTURE_NOT_FOUND");
        if (!await CanReadAsync(actorId, lecture.Course, ct))
            return Result<IReadOnlyList<LectureMaterialDto>>.Failure("FORBIDDEN");
        var rows = await db.CourseFiles.AsNoTracking()
            .Where(x => x.LectureId == lectureId && x.IsActive &&
                x.File!.Kind == FileKind.TrainingMaterial && x.File.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => new LectureMaterialDto(x.Id, x.FileId,
                x.File!.OriginalName, x.Title)).ToListAsync(ct);
        return Result<IReadOnlyList<LectureMaterialDto>>.Success(rows);
    }

    public async Task<Result<long>> AttachAsync(long actorId, long lectureId,
        long fileId, string? title, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "lecture.manage", ct))
            return Result<long>.Failure("FORBIDDEN");
        if (title?.Trim().Length > 300)
            return Result<long>.Failure("TITLE_TOO_LONG");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var lecture = await db.Lectures.Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
            if (lecture?.Course is null)
                return Result<long>.Failure("LECTURE_NOT_FOUND");
            if (!await CanManageAsync(actorId, lecture.Course, ct))
                return Result<long>.Failure("FORBIDDEN");
            var file = await db.StoredFiles.SingleOrDefaultAsync(x =>
                x.Id == fileId && x.IsActive &&
                x.Kind == FileKind.TrainingMaterial, ct);
            if (file is null) return Result<long>.Failure("FILE_NOT_FOUND");
            var effective = await permissions.GetEffectiveAsync(actorId, ct);
            if (file.UploadedBy != actorId &&
                !effective.HasMode(RoleCode.Admin))
                return Result<long>.Failure("FORBIDDEN");
            if (await db.CourseFiles.AnyAsync(x => x.LectureId == lectureId &&
                x.FileId == fileId && x.IsActive, ct))
                return Result<long>.Failure("MATERIAL_ALREADY_ATTACHED");
            var material = new CourseFile
            {
                CourseId = lecture.CourseId,
                LectureId = lectureId,
                FileId = fileId,
                Purpose = "TrainingMaterial",
                Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                IsActive = true,
                CreatedAt = clock.UtcNow,
                CreatedByUserId = actorId
            };
            db.CourseFiles.Add(material);
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Document,
                "lecture.material.attach", "ربط مادة بمحاضرة",
                "Lecture material attached", "CourseFile", material.Id,
                material.Title ?? file.OriginalName,
                CourseId: lecture.CourseId, ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(material.Id);
        }, ct);
    }

    private async Task<bool> CanReadAsync(long actorId, Course course, CancellationToken ct)
    {
        if (await CanManageAsync(actorId, course, ct)) return true;
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        return effective.HasMode(RoleCode.Member) && effective.Keys.Contains("page.member.enrolled") &&
            await db.CourseParticipants.AnyAsync(x => x.CourseId == course.Id &&
                x.UserId == actorId && x.IsActive, ct);
    }

    private async Task<bool> CanManageAsync(long actorId, Course course,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "lecture.manage", ct))
            return false;
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        return effective.HasMode(RoleCode.Admin) ||
            (effective.HasMode(RoleCode.Instructor) || effective.HasMode(RoleCode.ExternalInstructor)) && course.InstructorUserId == actorId ||
            course.ManagedByQuality && effective.HasMode(RoleCode.Quality);
    }
}
