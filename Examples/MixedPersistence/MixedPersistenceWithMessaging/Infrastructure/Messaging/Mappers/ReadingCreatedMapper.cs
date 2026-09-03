using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace MixedPersistenceWithMessaging;

public sealed class ReadingCreatedMapper : IIntegrationEventMapper<ReadingCreated>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(ReadingCreated domainEvent, DomainEventMetadata metadata) =>
        [new ReadingCreatedIntegrationEvent(domainEvent.ReadingId.Value, domainEvent.Value, metadata.EventId, metadata.OccurredAt)];
}
