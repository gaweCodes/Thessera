namespace GaWeCodes.Thessera.Domain.Rules;

/// <summary>
/// One thing that was wrong: the reason a rule refused an operation, in a shape that survives the
/// trip out of the domain.
/// </summary>
/// <remarks>
/// Both rule kinds collapse into this type, which is what lets several violations be reported
/// together instead of only the first one a caller happens to hit.
/// </remarks>
public sealed record RuleViolation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuleViolation"/> class.
    /// </summary>
    /// <param name="code">The stable identifier of the rule that was violated.</param>
    /// <param name="target">
    /// The field the complaint is about, or <see langword="null"/> when the violation is about the
    /// aggregate as a whole.
    /// </param>
    /// <param name="message">The human-readable explanation.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> or <paramref name="message"/> is <see langword="null"/>, empty or
    /// blank — a violation nobody can identify or read is not worth reporting. Or
    /// <paramref name="target"/> is empty or blank rather than <see langword="null"/>.
    /// </exception>
    public RuleViolation(string code, string? target, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (target is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(target);
        }

        Code = code;
        Target = target;
        Message = message;
    }

    /// <summary>
    /// Gets the stable identifier of the rule that was violated.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the field the complaint is about, or <see langword="null"/> when it names none.
    /// </summary>
    public string? Target { get; }

    /// <summary>
    /// Gets the human-readable explanation of what was wrong.
    /// </summary>
    public string Message { get; }
}
