using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[IntegrationEventTopic("mixed-persistence.reading-updated")]
public sealed record ReadingUpdatedIntegrationEvent(int ReadingId, int Value, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
