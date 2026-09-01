namespace GaWeCodes.Thessera.Domain.Events;

/// <summary>
/// The channel a child entity raises a domain event through, so that the event lands on its
/// aggregate root rather than on the child.
/// </summary>
/// <remarks>
/// A child has no event list of its own. It reaches its root through
/// <see cref="Entities.IChildOwner{TChildKey, TChildState}"/>, and the root raises on its behalf —
/// which is why a child hull built without a root would have nothing to raise into.
/// </remarks>
public interface IDomainEventRaiser
{
    /// <summary>
    /// Applies the event to the aggregate's state and records it as uncommitted.
    /// </summary>
    /// <param name="domainEvent">The event to raise. Never <see langword="null"/>.</param>
    void Raise(IDomainEvent domainEvent);
}
