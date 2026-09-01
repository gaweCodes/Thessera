namespace GaWeCodes.Thessera.Domain.Rules;

/// <summary>
/// A domain invariant that is either intact or broken, checked before the event that would break it
/// is raised.
/// </summary>
/// <remarks>
/// Use a business rule when the answer is about the state of the aggregate as a whole — "this
/// account may not be closed while it holds a balance". Use
/// <see cref="IDomainValidationRule"/> when it is about one input value.
/// <para>
/// A broken rule becomes a <see cref="BusinessRuleViolationException"/>. Whether it then also
/// becomes a failure in the <c>BusinessRule</c> category is runtime-dependent; see "What this
/// package promises" in the package README.
/// </para>
/// </remarks>
/// <seealso cref="RuleChecker"/>
public interface IBusinessRule
{
    /// <summary>
    /// Gets the stable identifier a caller can branch on.
    /// </summary>
    /// <value>
    /// A code that travels out of the service, so treat it as part of the contract: callers switch
    /// on it, and changing it changes their behaviour.
    /// </value>
    string Code { get; }

    /// <summary>
    /// Gets the human-readable explanation of what is wrong.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Determines whether the invariant is currently violated.
    /// </summary>
    /// <returns><see langword="true"/> when the rule is broken and the operation must not proceed.</returns>
    bool IsBroken();
}
