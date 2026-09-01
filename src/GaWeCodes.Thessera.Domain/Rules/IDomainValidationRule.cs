namespace GaWeCodes.Thessera.Domain.Rules;

/// <summary>
/// A rule about one input value, which can therefore name the field it is complaining about.
/// </summary>
/// <remarks>
/// The <see cref="Target"/> is what separates this from <see cref="IBusinessRule"/>: it lets an API
/// attach the message to a form field instead of reporting it against the whole request. Use a
/// validation rule when the answer points at one input, and a business rule when it is about the
/// aggregate as a whole.
/// <para>
/// A failing rule becomes a <see cref="DomainValidationException"/>. Whether it then also becomes
/// a failure in the <c>Validation</c> category is runtime-dependent; see "What this package
/// promises" in the package README.
/// </para>
/// </remarks>
/// <seealso cref="RuleChecker"/>
public interface IDomainValidationRule
{
    /// <summary>
    /// Gets the stable identifier a caller can branch on.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Gets the name of the field the complaint is about.
    /// </summary>
    /// <value>
    /// A field name an API can map onto its request shape, or <see langword="null"/> when the rule
    /// cannot name one.
    /// </value>
    string? Target { get; }

    /// <summary>
    /// Gets the human-readable explanation of what is wrong.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Determines whether the value currently fails this rule.
    /// </summary>
    /// <returns><see langword="true"/> when the value is invalid and the operation must not proceed.</returns>
    bool IsInvalid();
}
