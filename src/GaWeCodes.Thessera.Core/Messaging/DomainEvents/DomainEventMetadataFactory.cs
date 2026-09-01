using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

/// <summary>
/// Turns a stored envelope back into the metadata a projection handler or a mapper is given.
/// </summary>
public static class DomainEventMetadataFactory
{
    /// <summary>
    /// Reads the metadata out of an envelope.
    /// </summary>
    /// <param name="envelope">The stored envelope.</param>
    /// <returns>
    /// The same five fields the envelope carries alongside the payload, without the serialized
    /// event.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="envelope"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Used by the runtime when it dispatches a queued event, and by the testing package so a
    /// handler can be called directly with metadata identical in shape to the real thing.
    /// </remarks>
    public static DomainEventMetadata From(DomainEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new DomainEventMetadata(
            envelope.EventId,
            envelope.AggregateName,
            envelope.AggregateId,
            envelope.Version,
            envelope.OccurredAt);
    }
}
