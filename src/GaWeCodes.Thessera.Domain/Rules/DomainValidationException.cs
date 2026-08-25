namespace GaWeCodes.Thessera.Domain.Rules;

public sealed class DomainValidationException : Exception
{
    public const string FallbackCode = "domain.validation";

    public DomainValidationException()
        : base("The domain validation failed.")
    {
        Violations = [];
    }

    public DomainValidationException(string message)
        : base(message)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    public DomainValidationException(IReadOnlyList<RuleViolation> violations)
        : base(RuleViolationText.Describe(RuleViolationText.Validate(violations)))
    {
        Violations = violations;
    }

    public IReadOnlyList<RuleViolation> Violations { get; }
}
