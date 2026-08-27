using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace StateStoredWithMessaging;

[IntegrationEventTopic("state-readings.reading-created")]
public sealed record ReadingCreatedIntegrationEvent(int ReadingId, int Value, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
