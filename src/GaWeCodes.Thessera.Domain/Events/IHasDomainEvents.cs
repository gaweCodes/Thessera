namespace GaWeCodes.Thessera.Domain.Events;

/// <summary>
/// Something that has raised domain events and can hand them out.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>
    /// Gets the events raised since the last commit, in the order they were raised.
    /// </summary>
    /// <value>
    /// The uncommitted events, read by the unit of work at commit time. Writing one envelope per
    /// event into the outbox, and then clearing the collection through
    /// <see cref="IDomainEventOwner.ClearDomainEvents"/>, is runtime-dependent; see "What this
    /// package promises" in the package README.
    /// </value>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
}
