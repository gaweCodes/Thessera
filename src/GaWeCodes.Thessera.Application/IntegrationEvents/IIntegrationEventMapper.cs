using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Application.IntegrationEvents;

/// <summary>
/// Turns one domain event into the integration events other services should see — if any.
/// </summary>
/// <typeparam name="TDomainEvent">The domain event to map from.</typeparam>
/// <remarks>
/// This is the seam that keeps an internal model from leaking. A domain event may map to none, one
/// or several integration events, and the shape published is chosen here rather than inherited from
/// the domain.
/// <para>
/// Mappers are found by scanning the assemblies handed to the composition root. That discovery,
/// and the startup check that a mapper is reachable, are runtime-dependent; see "What this package
/// promises" in the package README.
/// </para>
/// </remarks>
public interface IIntegrationEventMapper<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>
    /// Maps the domain event.
    /// </summary>
    /// <param name="domainEvent">The event that happened.</param>
    /// <param name="metadata">
    /// Its context. Carrying <see cref="DomainEventMetadata.EventId"/> and
    /// <see cref="DomainEventMetadata.OccurredAt"/> over to the integration event keeps the two
    /// traceable to one another.
    /// </param>
    /// <returns>
    /// The events to publish, or an empty collection when this domain event is nobody else's
    /// business. Returning nothing is a normal answer, not an error.
    /// </returns>
    IReadOnlyCollection<IIntegrationEvent> Map(TDomainEvent domainEvent, DomainEventMetadata metadata);
}
