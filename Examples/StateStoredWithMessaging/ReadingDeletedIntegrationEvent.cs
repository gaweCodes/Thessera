using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace StateStoredWithMessaging;

[IntegrationEventTopic("state-readings.reading-deleted")]
public sealed record ReadingDeletedIntegrationEvent(int ReadingId, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
