namespace GaWeCodes.Thessera.Application.Results;

/// <summary>
/// The outcome of an operation that returns a value: either the value, or one or more failures.
/// </summary>
/// <typeparam name="TResult">
/// The value's type. It may not be <see cref="Failure"/> — see the remarks.
/// </typeparam>
/// <remarks>
/// Both a value and a <see cref="Failure"/> convert implicitly, so a handler returns whichever it
/// has and the compiler picks the right one.
/// <para>
/// <c>Result&lt;Failure&gt;</c> is rejected at construction. A failure is never a success value, and
/// both implicit conversions would apply to it, which makes every conversion ambiguous. Use the
/// non-generic <see cref="Result"/> for an operation with no return value.
/// </para>
/// </remarks>
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
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    private Result(IReadOnlyList<Failure> failures)
        : base(false, failures)
    {
        ThrowIfForbiddenResultType();
        _value = default!;
    }

    /// <summary>
    /// Gets the value the operation produced.
    /// </summary>
    /// <value>The value, available only on a successful result.</value>
    /// <exception cref="InvalidOperationException">
    /// The result is a failure. Check <see cref="Result.IsSuccess"/> first — a failed result has no
    /// value, and returning a default one would hide the failure.
    /// </exception>
    public TResult Value =>
        IsSuccess
            ? _value
            : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    /// <summary>
    /// Creates a successful result carrying a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A success.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TResult"/> is <see cref="Failure"/>.
    /// </exception>
    /// <remarks>Called through <see cref="Result.Success{TResult}(TResult)"/>; not a public entry point itself.</remarks>
    internal static Result<TResult> Success(TResult value) => new(value);

    /// <summary>
    /// Creates a failed result with one reason.
    /// </summary>
    /// <param name="failure">The reason.</param>
    /// <returns>A failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    /// <remarks>Called through <see cref="Result.Failed{TResult}(Failure)"/>; not a public entry point itself.</remarks>
    internal static new Result<TResult> Failed(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new Result<TResult>([failure]);
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
    /// <remarks>
    /// Called through <see cref="Result.Failed{TResult}(IReadOnlyList{Failure})"/>; not a public entry
    /// point itself.
    /// </remarks>
    internal static new Result<TResult> Failed(IReadOnlyList<Failure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return new Result<TResult>(failures);
    }

    /// <summary>
    /// Converts a value into a successful result, so a handler can simply return the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A successful result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA2225:Operator overloads have named alternates",
        Justification = "The named alternate the analyzer proposes for this generic conversion is a " +
            "method literally named \"FromTResult\", which names nothing real; Result.Success<TResult> " +
            "is the actual named alternative, already public on the non-generic Result.")]
    public static implicit operator Result<TResult>(TResult value) => Success(value);

    /// <summary>
    /// Converts a failure into a failed result, so a handler can simply return the failure.
    /// </summary>
    /// <param name="failure">The reason.</param>
    /// <returns>A failed result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA2225:Operator overloads have named alternates",
        Justification = "Failure.ToResult<TResult>() is the real named alternate, but the analyzer's " +
            "naming heuristic does not recognize a generic method as an alternate for a conversion into " +
            "a closed generic type.")]
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
