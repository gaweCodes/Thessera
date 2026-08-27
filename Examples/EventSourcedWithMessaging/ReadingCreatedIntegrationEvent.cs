using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace EventSourcedWithMessaging;

[IntegrationEventTopic("event-readings.reading-created")]
public sealed record ReadingCreatedIntegrationEvent(int ReadingId, int Value, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
