using System.Collections.ObjectModel;

namespace GaWeCodes.Thessera.Application.Results;

/// <summary>
/// The outcome of an operation that returns no value: either a success, or one or more failures —
/// never both.
/// </summary>
/// <remarks>
/// Expected outcomes travel in a result; unexpected ones stay exceptions. That line is what keeps
/// bugs visible: turning everything into a result would make a genuine defect look like an ordinary
/// answer.
/// </remarks>
/// <seealso cref="Result{TResult}"/>
public class Result
{
    private static readonly ReadOnlyCollection<Failure> NoFailures = new([]);

    private readonly ReadOnlyCollection<Failure> _failures;

    private protected Result(bool isSuccess, IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ThrowIfContainsNull(failures);

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

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the reasons the operation failed.
    /// </summary>
    /// <value>
    /// Empty on success, and never empty on failure. Several failures may be reported together, so
    /// a caller fixing one does not have to come back for the next.
    /// </value>
    public IReadOnlyList<Failure> Failures => _failures;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A success carrying no failures.</returns>
    public static Result Success() => new(true, NoFailures);

    /// <summary>
    /// Creates a successful result carrying a value.
    /// </summary>
    /// <typeparam name="TResult">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A success carrying <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static Result<TResult> Success<TResult>(TResult value)
        where TResult : notnull => Result<TResult>.Success(value);

    /// <summary>
    /// Creates a failed result carrying a value type, with one reason.
    /// </summary>
    /// <typeparam name="TResult">The value's type.</typeparam>
    /// <param name="failure">The reason.</param>
    /// <returns>A failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public static Result<TResult> Failed<TResult>(Failure failure)
        where TResult : notnull => Result<TResult>.Failed(failure);

    /// <summary>
    /// Creates a failed result carrying a value type, with several reasons.
    /// </summary>
    /// <typeparam name="TResult">The value's type.</typeparam>
    /// <param name="failures">The reasons. Must not be empty or contain <see langword="null"/>.</param>
    /// <returns>A failure carrying all of them.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="failures"/> is <see langword="null"/>, or contains a <see langword="null"/> entry.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="failures"/> is empty.</exception>
    public static Result<TResult> Failed<TResult>(IReadOnlyList<Failure> failures)
        where TResult : notnull => Result<TResult>.Failed(failures);

    /// <summary>
    /// Creates a failed result with one reason.
    /// </summary>
    /// <param name="failure">The reason.</param>
    /// <returns>A failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public static Result Failed(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new Result(false, [failure]);
    }

    /// <summary>
    /// Creates a failed result with several reasons.
    /// </summary>
    /// <param name="failures">The reasons. Must not be empty or contain <see langword="null"/>.</param>
    /// <returns>A failure carrying all of them.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="failures"/> is <see langword="null"/>, or contains a <see langword="null"/> entry.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="failures"/> is empty.</exception>
    public static Result Failed(IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return new Result(false, failures);
    }

    /// <summary>
    /// Converts a failure into a failed result, so a handler can simply return the failure.
    /// </summary>
    /// <param name="failure">The reason.</param>
    /// <returns>A failed result.</returns>
    public static implicit operator Result(Failure failure) => Failed(failure);

    /// <summary>
    /// Converts a failure into a failed result. The named alternative to the implicit conversion above.
    /// </summary>
    /// <param name="failure">The reason.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public static Result FromFailure(Failure failure) => Failed(failure);

    private static void ThrowIfContainsNull(IReadOnlyList<Failure> failures)
    {
        for (var index = 0; index < failures.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(failures[index], nameof(failures));
        }
    }
}
