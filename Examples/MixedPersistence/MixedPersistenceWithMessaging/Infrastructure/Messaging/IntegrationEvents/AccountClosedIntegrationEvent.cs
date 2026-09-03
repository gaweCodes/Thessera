using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[IntegrationEventTopic("mixed-persistence.account-closed")]
public sealed record AccountClosedIntegrationEvent(int AccountId, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
