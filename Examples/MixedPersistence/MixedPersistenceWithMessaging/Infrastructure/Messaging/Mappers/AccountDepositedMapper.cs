using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace MixedPersistenceWithMessaging;

public sealed class AccountDepositedMapper : IIntegrationEventMapper<AccountDeposited>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(AccountDeposited domainEvent, DomainEventMetadata metadata) =>
        [new AccountDepositedIntegrationEvent(domainEvent.AccountId.Value, domainEvent.Amount, metadata.EventId, metadata.OccurredAt)];
}
