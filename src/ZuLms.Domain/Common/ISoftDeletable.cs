namespace ZuLms.Domain.Common;

public interface ISoftDeletable
{
    bool IsActive { get; set; }
    DateTimeOffset? DeactivatedAt { get; set; }
    long? DeactivatedByUserId { get; set; }
    string? DeactivationReason { get; set; }
}

public static class SoftDeleteExtensions
{
    public static void Deactivate(this ISoftDeletable entity, long userId, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        entity.IsActive = false;
        entity.DeactivatedAt = now;
        entity.DeactivatedByUserId = userId;
        entity.DeactivationReason = reason;
    }

    public static void Restore(this ISoftDeletable entity)
    {
        entity.IsActive = true;
        entity.DeactivatedAt = null;
        entity.DeactivatedByUserId = null;
        entity.DeactivationReason = null;
    }
}
