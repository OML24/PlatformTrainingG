namespace ZuLms.Domain.Entities.Org;

public sealed class UniversitySide
{
    public int SideCode { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public bool IsImported { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
}
