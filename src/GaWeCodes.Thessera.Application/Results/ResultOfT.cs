namespace GaWeCodes.Thessera.Application.Results;

public sealed class Result<TResult> : Result
    where TResult : notnull
{
    private static readonly bool ForbiddenResultType = typeof(TResult) == typeof(Failure);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0032:Use auto property", Justification = "A backing field is required because the Value property validates the result state before returning the stored value")]
    private readonly TResult _value;

    private Result(TResult value)
        : base(true, [])
    {
        ThrowIfForbiddenResultType();
        _value = value;
    }

    private Result(IReadOnlyList<Failure> failures)
        : base(false, failures)
    {
        ThrowIfForbiddenResultType();
        _value = default!;
    }

    public TResult Value =>
        IsSuccess
            ? _value
            : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static Result<TResult> Success(TResult value) => new(value);

    public static new Result<TResult> Failed(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new Result<TResult>([failure]);
    }

    public static new Result<TResult> Failed(IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return new Result<TResult>(failures);
    }

    public static implicit operator Result<TResult>(TResult value) => Success(value);

    public static implicit operator Result<TResult>(Failure failure) => Failed(failure);

    private static void ThrowIfForbiddenResultType()
    {
        if (ForbiddenResultType)
        {
            throw new InvalidOperationException(
                "Result<Failure> is not a usable result type. A failure is never the success value of a " +
                "result, and both implicit conversions of Result<TResult> would apply to it, so every " +
                "conversion into it is ambiguous. Return the non-generic Result for an operation that has " +
                "no success value.");
        }
    }
}
