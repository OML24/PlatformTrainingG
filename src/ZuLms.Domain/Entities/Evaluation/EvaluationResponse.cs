using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Evaluation;

public sealed class EvaluationResponse
{
    public long Id { get; set; }
    public long CourseId { get; set; }
    public long ParticipantId { get; set; }
    public long VersionId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public Course? Course { get; set; }
    public CourseParticipant? Participant { get; set; }
    public EvaluationFormVersion? Version { get; set; }
}

