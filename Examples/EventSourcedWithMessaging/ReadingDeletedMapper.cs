using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace EventSourcedWithMessaging;

public sealed class ReadingDeletedMapper : IIntegrationEventMapper<ReadingDeleted>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(ReadingDeleted domainEvent, DomainEventMetadata metadata) =>
        [new ReadingDeletedIntegrationEvent(domainEvent.ReadingId.Value, metadata.EventId, metadata.OccurredAt)];
}
