using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace StateStoredWithMessaging;

[IntegrationEventTopic("state-readings.reading-updated")]
public sealed record ReadingUpdatedIntegrationEvent(int ReadingId, int Value, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
