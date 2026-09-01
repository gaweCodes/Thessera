using System.Text.Json;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

/// <summary>
/// Moves a domain event between its live form and the envelope it is stored and delivered in.
/// </summary>
/// <param name="registry">
/// The catalogue of persisted event names, used to write the name and to resolve one back.
/// </param>
/// <remarks>
/// Typed keys serialize as their bare value here, so an aggregate identity inside a stored event
/// reads as <c>"…"</c> rather than as an object — the same rendering the rest of the family uses.
/// </remarks>
public sealed class DomainEventEnvelopeSerializer(DomainEventTypeRegistry registry)
{
    private static readonly JsonSerializerOptions SerializerOptions = EntityKeyJsonOptions.Create();

    /// <summary>
    /// Serializes a domain event into an envelope.
    /// </summary>
    /// <param name="domainEvent">The event to wrap.</param>
    /// <param name="eventId">The identity to give this event.</param>
    /// <param name="aggregateName">The aggregate's persisted name.</param>
    /// <param name="aggregateId">The aggregate's identity, in the pinned stream-key format.</param>
    /// <param name="version">The aggregate version this event produced.</param>
    /// <param name="occurredAt">When the event happened.</param>
    /// <returns>The envelope, ready to be persisted, whether that is an outbox or something else.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The event's type is not in the catalogue, because its assembly was not registered with
    /// <c>AddDomainEventsFrom</c>. Its persisted name has to be known before it is written.
    /// </exception>
    public DomainEventEnvelope Wrap(
        IDomainEvent domainEvent,
        Guid eventId,
        string aggregateName,
        string aggregateId,
        long version,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventType = domainEvent.GetType();

        return new DomainEventEnvelope(
            registry.NameOf(eventType),
            JsonSerializer.Serialize(domainEvent, eventType, SerializerOptions),
            eventId,
            aggregateName,
            aggregateId,
            version,
            occurredAt);
    }

    /// <summary>
    /// Reads a stored envelope back into a domain event.
    /// </summary>
    /// <param name="envelope">The stored envelope.</param>
    /// <returns>The deserialized event, as its declared type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="envelope"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No type is registered under the envelope's event name, or the payload deserialized to
    /// <see langword="null"/>. A stored event whose name is no longer known cannot be read — keep a
    /// retired type and its name alongside the successor instead of renaming it.
    /// </exception>
    public IDomainEvent Unwrap(DomainEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var eventType = registry.Resolve(envelope.EventName);
        var domainEvent = JsonSerializer.Deserialize(envelope.Payload, eventType, SerializerOptions)
            ?? throw new InvalidOperationException(
                $"The domain event envelope payload deserialized to null for event name '{envelope.EventName}'.");

        return (IDomainEvent)domainEvent;
    }
}
