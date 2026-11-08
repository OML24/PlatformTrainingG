namespace ZuLms.Domain.Common;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public long? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public uint Version { get; set; }
}
