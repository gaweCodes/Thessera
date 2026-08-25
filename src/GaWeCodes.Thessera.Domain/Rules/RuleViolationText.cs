namespace GaWeCodes.Thessera.Domain.Rules;

internal static class RuleViolationText
{
    public const string Separator = "; ";

    public static IReadOnlyList<RuleViolation> Validate(IReadOnlyList<RuleViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        if (violations.Count == 0)
        {
            throw new ArgumentException(
                "A rule violation exception carries at least one violation. An empty list would report a failure " +
                "that names nothing, so the caller must not throw when every rule was satisfied.",
                nameof(violations));
        }

        for (var index = 0; index < violations.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(violations[index], nameof(violations));
        }

        return violations;
    }

    public static string Describe(IReadOnlyList<RuleViolation> violations) =>
        string.Join(Separator, violations.Select(violation => violation.Message));
}
