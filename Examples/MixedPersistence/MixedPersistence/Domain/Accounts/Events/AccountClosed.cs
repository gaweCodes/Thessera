using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistence;

[EventName("account-closed-v1")]
public sealed record AccountClosed(AccountId AccountId, DateTimeOffset OccurredAt) : DomainEvent;
