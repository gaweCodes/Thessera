using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace MixedPersistenceWithMessaging;

public sealed class ReadingUpdatedMapper : IIntegrationEventMapper<ReadingUpdated>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(ReadingUpdated domainEvent, DomainEventMetadata metadata) =>
        [new ReadingUpdatedIntegrationEvent(domainEvent.ReadingId.Value, domainEvent.Value, metadata.EventId, metadata.OccurredAt)];
}
