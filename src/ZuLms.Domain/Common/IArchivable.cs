namespace ZuLms.Domain.Common;

public interface IArchivable
{
    bool IsArchived { get; set; }
    DateTimeOffset? ArchivedAt { get; set; }
    long? ArchivedByUserId { get; set; }
}

public static class ArchiveExtensions
{
    public static void Archive(this IArchivable entity, long userId, DateTimeOffset now)
    {
        entity.IsArchived = true;
        entity.ArchivedAt = now;
        entity.ArchivedByUserId = userId;
    }

    public static void Unarchive(this IArchivable entity)
    {
        entity.IsArchived = false;
        entity.ArchivedAt = null;
        entity.ArchivedByUserId = null;
    }
}
