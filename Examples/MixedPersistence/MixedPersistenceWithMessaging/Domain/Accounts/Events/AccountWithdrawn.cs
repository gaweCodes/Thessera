using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[EventName("account-withdrawn-v1")]
public sealed record AccountWithdrawn(AccountId AccountId, decimal Amount, DateTimeOffset OccurredAt) : DomainEvent;
