using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace MixedPersistenceWithMessaging;

public sealed class AccountOpenedMapper : IIntegrationEventMapper<AccountOpened>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(AccountOpened domainEvent, DomainEventMetadata metadata) =>
        [new AccountOpenedIntegrationEvent(domainEvent.AccountId.Value, domainEvent.InitialBalance, metadata.EventId, metadata.OccurredAt)];
}
