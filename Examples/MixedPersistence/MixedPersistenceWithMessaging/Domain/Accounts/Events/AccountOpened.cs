using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[EventName("account-opened-v1")]
public sealed record AccountOpened(AccountId AccountId, decimal InitialBalance, DateTimeOffset OccurredAt) : DomainEvent;
