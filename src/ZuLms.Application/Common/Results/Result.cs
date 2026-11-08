namespace ZuLms.Application.Common.Results;

public class Result
{
    protected Result(bool ok, Error? error) { Ok = ok; Error = error; }
    public bool Ok { get; }
    public Error? Error { get; }
    public static Result Success() => new(true, null);
    public static Result Failure(string code, string? message = null) => new(false, new Error(code, message));
}

public sealed class Result<T> : Result
{
    private Result(bool ok, T? value, Error? error) : base(ok, error) { Value = value; }
    public T? Value { get; }
    public static Result<T> Success(T value) => new(true, value, null);
    public new static Result<T> Failure(string code, string? message = null) => new(false, default, new Error(code, message));
}
