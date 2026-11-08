using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class ExamResultHistory
{
    public long Id { get; set; }
    public long ExamResultId { get; set; }
    public ExamOutcome? OldOutcome { get; set; }
    public ExamOutcome NewOutcome { get; set; }
    public long ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Note { get; set; }
    public ExamResult? ExamResult { get; set; }
    public User? ChangedByUser { get; set; }
}

