using System.Collections.ObjectModel;

namespace GaWeCodes.Thessera.Application.Results;

public class Result
{
    private static readonly ReadOnlyCollection<Failure> NoFailures = new([]);

    private readonly ReadOnlyCollection<Failure> _failures;

    private protected Result(bool isSuccess, IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        if (isSuccess && failures.Count > 0)
        {
            throw new ArgumentException("A successful result cannot carry failures.", nameof(failures));
        }

        if (!isSuccess && failures.Count == 0)
        {
            throw new ArgumentException("A failed result must carry at least one failure.", nameof(failures));
        }

        IsSuccess = isSuccess;
        _failures = failures.Count == 0 ? NoFailures : new ReadOnlyCollection<Failure>([.. failures]);
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IReadOnlyList<Failure> Failures => _failures;

    public static Result Success() => new(true, NoFailures);

    public static Result<TResult> Success<TResult>(TResult value)
        where TResult : notnull => Result<TResult>.Success(value);

    public static Result Failed(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new Result(false, [failure]);
    }

    public static Result Failed(IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return new Result(false, failures);
    }

    public static implicit operator Result(Failure failure) => Failed(failure);
}
