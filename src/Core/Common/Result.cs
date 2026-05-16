namespace KnowledgeSearch.Core.Common;

public class Result
{
    private protected Result(ResultError? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    public ResultError? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(string code, string message) =>
        new(new ResultError(code, message, ErrorKind.Failure));

    public static Result Validation(string message, string code = "validation") =>
        new(new ResultError(code, message, ErrorKind.Validation));

    public static Result NotFound(string message, string code = "not_found") =>
        new(new ResultError(code, message, ErrorKind.NotFound));

    public static Result Conflict(string message, string code = "conflict") =>
        new(new ResultError(code, message, ErrorKind.Conflict));

    public static Result Unauthorized(string message, string code = "unauthorized") =>
        new(new ResultError(code, message, ErrorKind.Unauthorized));

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(string code, string message) =>
        new(default, new ResultError(code, message, ErrorKind.Failure));

    public static Result<T> Validation<T>(string message, string code = "validation") =>
        new(default, new ResultError(code, message, ErrorKind.Validation));

    public static Result<T> NotFound<T>(string message, string code = "not_found") =>
        new(default, new ResultError(code, message, ErrorKind.NotFound));

    public static Result<T> Conflict<T>(string message, string code = "conflict") =>
        new(default, new ResultError(code, message, ErrorKind.Conflict));

    public static Result<T> Unauthorized<T>(string message, string code = "unauthorized") =>
        new(default, new ResultError(code, message, ErrorKind.Unauthorized));
}

public sealed class Result<T> : Result
{
    internal Result(T? value, ResultError? error) : base(error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(ResultError error) => new(default, error);
}
