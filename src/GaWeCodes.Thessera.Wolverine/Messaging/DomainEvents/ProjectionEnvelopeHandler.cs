using GaWeCodes.Thessera.Core.Messaging.DomainEvents;

namespace GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;

/// <summary>
/// Takes a domain event off the projection queue and runs every projection handler registered for
/// it.
/// </summary>
/// <param name="projectionRunner">Dispatches the event to the handlers registered for its type.</param>
/// <param name="serializer">Turns the stored envelope back into the domain event.</param>
/// <remarks>
/// Projections have their own durable queue, behind the one that publishes integration events, so a
/// slow projection cannot hold up domain-event delivery. Both queues partition by the aggregate, so
/// one aggregate's events are projected in order while different aggregates proceed in parallel.
/// <para>
/// Public because the message engine discovers and invokes it; a consumer never calls it.
/// </para>
/// </remarks>
public sealed class ProjectionEnvelopeHandler(
    ProjectionRunner projectionRunner,
    DomainEventEnvelopeSerializer serializer)
{
    /// <summary>
    /// Handles one queued projection envelope.
    /// </summary>
    /// <param name="envelope">The stored domain event, with its metadata.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once every projection handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="envelope"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Delivery is at-least-once, so this may run twice for the same event. A projection handler is
    /// expected to be idempotent, using the aggregate version on the metadata as its watermark.
    /// </remarks>
    public Task Handle(ProjectionEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var domainEvent = serializer.Unwrap(envelope.Event);
        var metadata = DomainEventMetadataFactory.From(envelope.Event);

        return projectionRunner.RunAsync(domainEvent, metadata, cancellationToken);
    }
}
