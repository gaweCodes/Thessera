using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[IntegrationEventTopic("mixed-persistence.account-opened")]
public sealed record AccountOpenedIntegrationEvent(int AccountId, decimal InitialBalance, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
