namespace GaWeCodes.Thessera.Domain.Events;

/// <summary>
/// An aggregate that owns its uncommitted domain events, and can be told that they have been
/// persisted.
/// </summary>
/// <remarks>
/// Clearing is explicitly separated from reading so that the events survive a failed commit: they
/// are dropped only once the transaction that wrote them has succeeded.
/// </remarks>
public interface IDomainEventOwner : IHasDomainEvents
{
    /// <summary>
    /// Drops the uncommitted domain events.
    /// </summary>
    /// <remarks>
    /// Called by the unit of work after a successful commit, and by nothing else. Calling it
    /// yourself risks losing events nobody has durably published yet — whether that is an outbox
    /// at all is runtime-dependent; see "What this package promises" in the package README.
    /// </remarks>
    void ClearDomainEvents();
}
