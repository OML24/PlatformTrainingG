using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Config;

public sealed class FieldHint : BaseEntity, ISoftDeletable
{
    public string FieldKey { get; set; } = string.Empty;
    public string ContextKey { get; set; } = string.Empty;
    public string HintAr { get; set; } = string.Empty;
    public string? HintEn { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
}

