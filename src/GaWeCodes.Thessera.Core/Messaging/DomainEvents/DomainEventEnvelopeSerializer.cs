using System.Text.Json;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

public sealed class DomainEventEnvelopeSerializer(DomainEventTypeRegistry registry)
{
    private static readonly JsonSerializerOptions SerializerOptions = EntityKeyJsonOptions.Create();

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
