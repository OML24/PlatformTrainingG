namespace ZuLms.Domain.Common;

public sealed class DomainException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
