namespace GaWeCodes.Thessera.Application.Results;

/// <summary>
/// The coarse kind of a failure, so that a caller can map it without knowing every code.
/// </summary>
/// <remarks>
/// <strong>This enum is allowed to gain members in a minor version.</strong> That is a deliberate
/// trade: a closed enum would force a major version for every failure kind the family ever learns.
/// It means a <c>switch</c> over a category must carry a <c>_</c> arm, and that arm should map to a
/// generic server-side failure rather than throw. Code without one compiles today and breaks on an
/// upgrade that is otherwise not breaking.
/// <para>
/// The category is deliberately not an HTTP status. Mapping it to one belongs to the host, which
/// knows its own protocol; the domain does not.
/// </para>
/// </remarks>
public enum FailureCategory
{
    /// <summary>
    /// An input value was wrong. Usually carries a <see cref="Failure.Target"/> naming the field.
    /// </summary>
    Validation,

    /// <summary>
    /// A domain invariant refused the operation. The inputs were fine; the state did not allow it.
    /// </summary>
    BusinessRule,

    /// <summary>
    /// What the request addressed does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The write collided with another one — a unique constraint, or a concurrent change to the
    /// same aggregate. Retrying with fresh state is often the right answer.
    /// </summary>
    Conflict,

    /// <summary>
    /// The caller is not allowed to do this. Distinct from <see cref="NotFound"/> on purpose: which
    /// of the two a service reveals is a decision about what it is willing to disclose.
    /// </summary>
    Forbidden,
}
