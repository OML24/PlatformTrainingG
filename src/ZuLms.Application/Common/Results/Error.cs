namespace ZuLms.Application.Common.Results;

public sealed record Error(string Code, string? Message = null);
