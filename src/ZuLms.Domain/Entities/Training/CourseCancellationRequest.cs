using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class CourseCancellationRequest : BaseEntity
{
    public long CourseId { get; set; }
    public long RequestedBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? LetterFileId { get; set; }
    public RequestStatus Status { get; set; }
    public long? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
    public Course? Course { get; set; }
    public User? RequestedByUser { get; set; }
    public StoredFile? LetterFile { get; set; }
    public User? DecidedByUser { get; set; }
}

