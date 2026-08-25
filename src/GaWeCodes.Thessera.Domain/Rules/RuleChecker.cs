namespace GaWeCodes.Thessera.Domain.Rules;

public static class RuleChecker
{
    public static void CheckBusinessRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.IsBroken())
        {
            throw new BusinessRuleViolationException([Violation(rule)]);
        }
    }

    public static void CheckValidationRule(IDomainValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.IsInvalid())
        {
            throw new DomainValidationException([Violation(rule)]);
        }
    }

    public static void CheckAllBusinessRules(params IBusinessRule[] rules)
    {
        GuardAll(rules);

        var violations = new List<RuleViolation>();
        foreach (var rule in rules)
        {
            if (rule.IsBroken())
            {
                violations.Add(Violation(rule));
            }
        }

        if (violations.Count > 0)
        {
            throw new BusinessRuleViolationException(violations);
        }
    }

    public static void CheckAllValidationRules(params IDomainValidationRule[] rules)
    {
        GuardAll(rules);

        var violations = new List<RuleViolation>();
        foreach (var rule in rules)
        {
            if (rule.IsInvalid())
            {
                violations.Add(Violation(rule));
            }
        }

        if (violations.Count > 0)
        {
            throw new DomainValidationException(violations);
        }
    }

    private static void GuardAll<TRule>(TRule[] rules)
        where TRule : class
    {
        ArgumentNullException.ThrowIfNull(rules);

        for (var index = 0; index < rules.Length; index++)
        {
            ArgumentNullException.ThrowIfNull(rules[index], nameof(rules));
        }
    }

    private static RuleViolation Violation(IBusinessRule rule) => new(rule.Code, null, rule.Message);

    private static RuleViolation Violation(IDomainValidationRule rule) =>
        new(rule.Code, rule.Target, rule.Message);
}
