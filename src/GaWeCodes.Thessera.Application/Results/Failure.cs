namespace GaWeCodes.Thessera.Application.Results;

/// <summary>
/// One reason an operation did not succeed, in a shape a caller can branch on and a person can
/// read.
/// </summary>
/// <remarks>
/// Persistence contributes its own failures rather than letting driver exceptions escape — a
/// unique violation or a concurrency conflict can arrive here as a conflict. Whether it does is
/// runtime-dependent; see "What this package promises" in the package README.
/// </remarks>
public sealed record Failure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Failure"/> class.
    /// </summary>
    /// <param name="code">
    /// The stable identifier a caller branches on. It leaves the service, so treat it as part of
    /// the contract.
    /// </param>
    /// <param name="message">The human-readable explanation.</param>
    /// <param name="category">The coarse kind of failure.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> or <paramref name="message"/> is <see langword="null"/>, empty or
    /// blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="category"/> is not one of the declared values.
    /// </exception>
    /// <remarks>
    /// Prefer the named factory methods; this constructor exists for the cases they do not cover.
    /// </remarks>
    public Failure(string code, string message, FailureCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "The failure category must be one of the declared values, because the transport layer maps it to a status code.");
        }

        Code = code;
        Message = message;
        Category = category;
    }

    /// <summary>
    /// Gets the stable identifier a caller branches on.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable explanation of what went wrong.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the coarse kind of failure, for callers that map rather than branch.
    /// </summary>
    public FailureCategory Category { get; }

    /// <summary>
    /// Gets the field this failure is about, when it is about one.
    /// </summary>
    /// <value>
    /// A field name the caller can attach the message to, or <see langword="null"/>. Usually set
    /// for <see cref="FailureCategory.Validation"/> and left unset otherwise.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value assigned is empty or blank rather than <see langword="null"/>.
    /// </exception>
    public string? Target
    {
        get;
        init => field = value is null || !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("The target must be null, or a non-blank field name.", nameof(value));
    }

    /// <summary>Creates a failure about a wrong input value.</summary>
    /// <param name="code">The stable identifier.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>A failure in the <see cref="FailureCategory.Validation"/> category.</returns>
    public static Failure Validation(string code, string message) => new(code, message, FailureCategory.Validation);

    /// <summary>Creates a failure about a refused domain invariant.</summary>
    /// <param name="code">The stable identifier.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>A failure in the <see cref="FailureCategory.BusinessRule"/> category.</returns>
    public static Failure BusinessRule(string code, string message) => new(code, message, FailureCategory.BusinessRule);

    /// <summary>Creates a failure about something that does not exist.</summary>
    /// <param name="code">The stable identifier.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>A failure in the <see cref="FailureCategory.NotFound"/> category.</returns>
    public static Failure NotFound(string code, string message) => new(code, message, FailureCategory.NotFound);

    /// <summary>Creates a failure about a collision with another write.</summary>
    /// <param name="code">The stable identifier.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>A failure in the <see cref="FailureCategory.Conflict"/> category.</returns>
    public static Failure Conflict(string code, string message) => new(code, message, FailureCategory.Conflict);

    /// <summary>Creates a failure about an operation the caller may not perform.</summary>
    /// <param name="code">The stable identifier.</param>
    /// <param name="message">The human-readable explanation.</param>
    /// <returns>A failure in the <see cref="FailureCategory.Forbidden"/> category.</returns>
    public static Failure Forbidden(string code, string message) => new(code, message, FailureCategory.Forbidden);

    /// <summary>
    /// Converts this failure into a failed <see cref="Result{TResult}"/>. The named alternative to the
    /// implicit conversion on <see cref="Result{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The value type the result would have carried on success.</typeparam>
    /// <returns>A failed result carrying this failure.</returns>
    public Result<TResult> ToResult<TResult>()
        where TResult : notnull => Result.Failed<TResult>(this);
}
