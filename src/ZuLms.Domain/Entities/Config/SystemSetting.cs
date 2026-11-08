using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Config;

public sealed class SystemSetting
{
    public uint Version { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public SettingValueType ValueType { get; set; }
    public string Value { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string LabelAr { get; set; } = string.Empty;
    public string? LabelEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsSecret { get; set; }
    public int SortOrder { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public User? UpdatedByUser { get; set; }
}

