namespace GaWeCodes.Thessera.Domain;

/// <summary>
/// The current time, taken as a dependency so that a domain decision which depends on "now" can be
/// tested without waiting for it.
/// </summary>
/// <remarks>
/// The family uses this to stamp <c>OccurredAt</c> on domain-event envelopes. A domain model that
/// calls <see cref="DateTimeOffset.UtcNow"/> directly is testable only by contriving the clock of
/// the machine running the test.
/// </remarks>
public interface IClock
{
    /// <summary>
    /// Gets the current instant, with its offset.
    /// </summary>
    /// <value>
    /// An offset-aware timestamp. Implementations should return UTC, because the value is persisted
    /// and compared across services.
    /// </value>
    DateTimeOffset Now { get; }
}
