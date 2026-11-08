using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Config;

public sealed class NumberSequence
{
    public string SequenceKey { get; set; } = string.Empty;
    public long LastValue { get; set; }
}

