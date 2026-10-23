using System.Diagnostics.CodeAnalysis;

namespace Attendance.Shared.Results;

/// <summary>The outcome of an operation that has no value: success, or an expected <see cref="Error"/>.</summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);
}

/// <summary>The outcome of an operation that produces a <typeparamref name="T"/> on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null) => _value = value;

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>The value. Reading it from a failed result is a programming error and throws.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value (error '{Error.Code}').");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public static implicit operator Result<T>(T value) => Success(value);
}
