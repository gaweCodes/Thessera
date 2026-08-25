namespace GaWeCodes.Thessera.Domain.Rules;

public sealed class BusinessRuleViolationException : Exception
{
    public const string FallbackCode = "domain.business_rule";

    public BusinessRuleViolationException()
        : base("A business rule was violated.")
    {
        Violations = [];
    }

    public BusinessRuleViolationException(string message)
        : base(message)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    public BusinessRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Violations = [new RuleViolation(FallbackCode, null, message)];
    }

    public BusinessRuleViolationException(IReadOnlyList<RuleViolation> violations)
        : base(RuleViolationText.Describe(RuleViolationText.Validate(violations)))
    {
        Violations = violations;
    }

    public IReadOnlyList<RuleViolation> Violations { get; }
}
