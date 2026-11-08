using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class CourseProposalRevision
{
    public long Id { get; set; }
    public long CourseId { get; set; }
    public int RevisionNo { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; }
    public long SubmittedBy { get; set; }
    public ProposalDecision Decision { get; set; }
    public long? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
    public Course? Course { get; set; }
    public User? SubmittedByUser { get; set; }
    public User? DecidedByUser { get; set; }
}

