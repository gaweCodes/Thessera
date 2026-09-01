using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Wolverine.Messaging.Transport;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;

/// <summary>
/// Takes a domain event off the outbox queue, publishes whatever the integration-event mappers make
/// of it, and then forwards it to the projection queue.
/// </summary>
/// <param name="publisher">Runs the mappers registered for the event.</param>
/// <param name="serializer">Turns the stored envelope back into the domain event.</param>
/// <param name="sinkFactory">Builds the sink the mapped events are handed to.</param>
/// <remarks>
/// This is where a committed domain event turns into everything that follows it. Integration events
/// go first and projections second, on their own queue, so a slow projection cannot hold up
/// delivery to other services.
/// <para>
/// Public because the message engine discovers and invokes it; a consumer never calls it.
/// </para>
/// </remarks>
public sealed class DomainEventEnvelopeHandler(
    IIntegrationEventPublisher publisher,
    DomainEventEnvelopeSerializer serializer,
    IIntegrationEventSinkFactory sinkFactory)
{
    /// <summary>
    /// Handles one queued domain event.
    /// </summary>
    /// <param name="envelope">The stored domain event, with its metadata.</param>
    /// <param name="context">The message context, used to publish onwards within the same session.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A task that completes once the mapped integration events have been handed to the sink and the
    /// projection envelope has been queued.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="envelope"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// With no transport configured the sink is the fallback that logs a warning per discarded
    /// integration event — the projection half still runs, which is why a service without a broker
    /// keeps working.
    /// </remarks>
    public async Task Handle(DomainEventEnvelope envelope, IMessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(context);

        var domainEvent = serializer.Unwrap(envelope);
        var metadata = DomainEventMetadataFactory.From(envelope);
        var emitter = new WolverineMessageEmitter(context);

        await publisher.PublishAsync(domainEvent, metadata, sinkFactory.Create(emitter), cancellationToken)
            .ConfigureAwait(false);

        await emitter.PublishAsync(new ProjectionEnvelope(envelope), null, cancellationToken).ConfigureAwait(false);
    }
}
