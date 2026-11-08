using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class AttendanceRecord
{
    public long Id { get; set; }
    public long LectureId { get; set; }
    public long ParticipantId { get; set; }
    public bool IsPresent { get; set; }
    public AttendanceSource Source { get; set; }
    public DateTimeOffset MarkedAt { get; set; }
    public long? MarkedBy { get; set; }
    public Lecture? Lecture { get; set; }
    public CourseParticipant? Participant { get; set; }
    public User? MarkedByUser { get; set; }
}

