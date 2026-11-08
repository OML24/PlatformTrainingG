using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class AttendanceCodeAttempt
{
    public long Id { get; set; }
    public long LectureId { get; set; }
    public long UserId { get; set; }
    public int WrongCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }
    public Lecture? Lecture { get; set; }
    public User? User { get; set; }
}

