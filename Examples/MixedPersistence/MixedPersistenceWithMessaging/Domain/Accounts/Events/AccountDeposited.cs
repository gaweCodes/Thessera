using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[EventName("account-deposited-v1")]
public sealed record AccountDeposited(AccountId AccountId, decimal Amount, DateTimeOffset OccurredAt) : DomainEvent;
