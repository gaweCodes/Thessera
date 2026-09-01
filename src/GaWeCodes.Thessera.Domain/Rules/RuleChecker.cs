namespace GaWeCodes.Thessera.Domain.Rules;

/// <summary>
/// Checks rules and turns the broken ones into the exception the dispatcher knows how to convert
/// into a failed result.
/// </summary>
/// <remarks>
/// Call these from the aggregate's factory or from the method that is about to raise an event —
/// before the event exists. Once an event has been raised it is a fact that has happened, and
/// refusing it afterwards would leave the aggregate unable to replay its own history.
/// <para>
/// The <c>CheckAll…</c> overloads report every violation of a run, so a caller fixing one problem
/// does not have to come back for the next.
/// </para>
/// </remarks>
public static class RuleChecker
{
    /// <summary>
    /// Throws when a single business rule is broken.
    /// </summary>
    /// <param name="rule">The rule to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    /// <exception cref="BusinessRuleViolationException">
    /// The rule is broken. The violation carries the rule's own <see cref="IBusinessRule.Code"/>
    /// and message.
    /// </exception>
    public static void CheckBusinessRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.IsBroken())
        {
            throw new BusinessRuleViolationException([Violation(rule)]);
        }
    }

    /// <summary>
    /// Throws when a single validation rule fails.
    /// </summary>
    /// <param name="rule">The rule to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    /// <exception cref="DomainValidationException">
    /// The rule failed. The violation carries the rule's own
    /// <see cref="IDomainValidationRule.Code"/>, its <see cref="IDomainValidationRule.Target"/> and
    /// its message.
    /// </exception>
    public static void CheckValidationRule(IDomainValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.IsInvalid())
        {
            throw new DomainValidationException([Violation(rule)]);
        }
    }

    /// <summary>
    /// Checks every business rule and throws once, reporting all the broken ones together.
    /// </summary>
    /// <param name="rules">The rules to check, in the order they should be reported.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rules"/> is <see langword="null"/> or contains a <see langword="null"/>
    /// entry.
    /// </exception>
    /// <exception cref="BusinessRuleViolationException">
    /// At least one rule is broken. Every broken rule is in
    /// <see cref="BusinessRuleViolationException.Violations"/>.
    /// </exception>
    /// <remarks>
    /// All rules are evaluated, so each one has to be safe to evaluate even when an earlier one is
    /// already broken.
    /// </remarks>
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

    /// <summary>
    /// Checks every validation rule and throws once, reporting all the failing ones together.
    /// </summary>
    /// <param name="rules">The rules to check, in the order they should be reported.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rules"/> is <see langword="null"/> or contains a <see langword="null"/>
    /// entry.
    /// </exception>
    /// <exception cref="DomainValidationException">
    /// At least one rule failed. Every failing rule is in
    /// <see cref="DomainValidationException.Violations"/>, each with the field it names.
    /// </exception>
    /// <remarks>
    /// All rules are evaluated, so each one has to be safe to evaluate even when an earlier one has
    /// already failed.
    /// </remarks>
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
