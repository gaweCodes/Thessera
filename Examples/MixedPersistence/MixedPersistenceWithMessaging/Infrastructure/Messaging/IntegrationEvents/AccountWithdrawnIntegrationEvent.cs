using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[IntegrationEventTopic("mixed-persistence.account-withdrawn")]
public sealed record AccountWithdrawnIntegrationEvent(int AccountId, decimal Amount, Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
