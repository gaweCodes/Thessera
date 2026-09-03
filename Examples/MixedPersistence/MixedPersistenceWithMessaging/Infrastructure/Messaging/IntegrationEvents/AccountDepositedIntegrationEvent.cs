using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[IntegrationEventTopic("mixed-persistence.account-deposited")]
public sealed record AccountDepositedIntegrationEvent(int AccountId, decimal Amount, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
