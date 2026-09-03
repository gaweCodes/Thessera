using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[IntegrationEventTopic("mixed-persistence.reading-created")]
public sealed record ReadingCreatedIntegrationEvent(int ReadingId, int Value, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
