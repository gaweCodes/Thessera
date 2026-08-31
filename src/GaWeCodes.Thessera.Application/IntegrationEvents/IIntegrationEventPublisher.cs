using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Application.IntegrationEvents;

/// <summary>
/// Runs the mappers registered for a domain event and hands whatever they produce to a sink.
/// </summary>
/// <remarks>
/// Implemented by the runtime; you write mappers rather than a publisher. It is part of the public
/// surface because a transport or runtime adapter has to be able to drive it.
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Maps one domain event and publishes the results.
    /// </summary>
    /// <param name="domainEvent">The event that happened.</param>
    /// <param name="metadata">Its context, passed on to each mapper.</param>
    /// <param name="integrationEventSink">
    /// Where the mapped events go — the transport's sink, or the fallback that logs and discards
    /// when no transport is configured.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once every mapped event has been handed to the sink.</returns>
    Task PublishAsync(IDomainEvent domainEvent, DomainEventMetadata metadata, IIntegrationEventSink integrationEventSink, CancellationToken cancellationToken);
}
