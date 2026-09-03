namespace GaWeCodes.Thessera.Domain.Rules;

/// <summary>
/// Thrown when an input value failed a domain validation rule.
/// </summary>
/// <remarks>
/// Raise it through <see cref="RuleChecker"/> rather than directly, so that the violations carry
/// the rule's own code and the field it names. Being caught and turned into a failed result in the
/// <c>Validation</c> category is runtime-dependent; see "What this package promises" in the
/// package README.
/// </remarks>
/// <seealso cref="BusinessRuleViolationException"/>
public sealed class DomainValidationException : Exception
{
    /// <summary>
    /// The code used when the exception was raised from a message rather than from a rule, and no
    /// rule code is therefore available.
    /// </summary>
    public const string FallbackCode = "domain.validation";

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainValidationException"/> class with a
    /// generic message and no violations.
    /// </summary>
    public DomainValidationException()
        : base("The domain validation failed.")
    {
        Violations = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainValidationException"/> class with a
    /// message.
    /// </summary>
    /// <param name="message">The explanation of what was invalid.</param>
    /// <remarks>
    /// <see cref="Violations"/> gets a single entry carrying <see cref="FallbackCode"/> and no
    /// target, because no rule code or field name is available on this path.
    /// </remarks>
    public DomainValidationException(string message)
        : base(message)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainValidationException"/> class with a
    /// message and the exception that caused it.
    /// </summary>
    /// <param name="message">The explanation of what was invalid.</param>
    /// <param name="innerException">The underlying cause.</param>
    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainValidationException"/> class from the
    /// violations that were found.
    /// </summary>
    /// <param name="violations">
    /// Every rule that failed, not just the first. Must not be empty or contain
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="violations"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="violations"/> is empty or contains a <see langword="null"/> entry, which
    /// would produce an exception that reports nothing.
    /// </exception>
    public DomainValidationException(IReadOnlyList<RuleViolation> violations)
        : base(RuleViolationText.Describe(RuleViolationText.Validate(violations)))
    {
        Violations = [.. violations];
    }

    /// <summary>
    /// Gets every violation that was found, in the order the rules were checked.
    /// </summary>
    /// <value>
    /// Empty only for the parameterless constructor. Each entry may name a
    /// <see cref="RuleViolation.Target"/>, which is what lets an API attach the message to a field.
    /// </value>
    public IReadOnlyList<RuleViolation> Violations { get; }
}
