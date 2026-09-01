namespace GaWeCodes.Thessera.Domain.Rules;

/// <summary>
/// Thrown when a domain invariant refused an operation.
/// </summary>
/// <remarks>
/// Raise it through <see cref="RuleChecker"/> rather than directly, so that the violations carry
/// the rule's own code instead of the fallback. Being caught and turned into a failed result in
/// the <c>BusinessRule</c> category is runtime-dependent; see "What this package promises" in the
/// package README.
/// </remarks>
/// <seealso cref="DomainValidationException"/>
public sealed class BusinessRuleViolationException : Exception
{
    /// <summary>
    /// The code used when the exception was raised from a message rather than from a rule, and no
    /// rule code is therefore available.
    /// </summary>
    public const string FallbackCode = "domain.business_rule";

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class with a
    /// generic message and no violations.
    /// </summary>
    public BusinessRuleViolationException()
        : base("A business rule was violated.")
    {
        Violations = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class with a
    /// message.
    /// </summary>
    /// <param name="message">The explanation of what was refused.</param>
    /// <remarks>
    /// <see cref="Violations"/> gets a single entry carrying <see cref="FallbackCode"/>, because no
    /// rule code is available on this path.
    /// </remarks>
    public BusinessRuleViolationException(string message)
        : base(message)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class with a
    /// message and the exception that caused it.
    /// </summary>
    /// <param name="message">The explanation of what was refused.</param>
    /// <param name="innerException">The underlying cause.</param>
    public BusinessRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class from the
    /// violations that were found.
    /// </summary>
    /// <param name="violations">
    /// Every rule that was broken, not just the first. Must not be empty or contain
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="violations"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="violations"/> is empty or contains a <see langword="null"/> entry, which
    /// would produce an exception that reports nothing.
    /// </exception>
    public BusinessRuleViolationException(IReadOnlyList<RuleViolation> violations)
        : base(RuleViolationText.Describe(RuleViolationText.Validate(violations)))
    {
        Violations = violations;
    }

    /// <summary>
    /// Gets every violation that was found, in the order the rules were checked.
    /// </summary>
    /// <value>
    /// Empty only for the parameterless constructor. Reporting all of them at once spares a caller
    /// the round trip of fixing one problem to discover the next.
    /// </value>
    public IReadOnlyList<RuleViolation> Violations { get; }
}
