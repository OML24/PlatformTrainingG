using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class AttendanceChange
{
    public long Id { get; set; }
    public long LectureId { get; set; }
    public long ParticipantId { get; set; }
    public bool? OldPresent { get; set; }
    public bool NewPresent { get; set; }
    public AttendanceSource Source { get; set; }
    public long? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Reason { get; set; }
    public Lecture? Lecture { get; set; }
    public CourseParticipant? Participant { get; set; }
    public User? ChangedByUser { get; set; }
}

