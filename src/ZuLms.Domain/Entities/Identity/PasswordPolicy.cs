namespace ZuLms.Domain.Entities.Identity;

public sealed class PasswordPolicy
{
    public short Id { get; set; } = 1;
    public int MinLength { get; set; }
    public int MaxLength { get; set; }
    public bool AllowSpaces { get; set; }
    public bool AllowArabic { get; set; }
    public bool BlockCommon { get; set; }
    public bool BlockContext { get; set; }
    public bool RequireUpper { get; set; }
    public bool RequireLower { get; set; }
    public bool RequireDigit { get; set; }
    public bool RequireSymbol { get; set; }
    public int? RotationDays { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
