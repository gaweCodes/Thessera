using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace MixedPersistenceWithMessaging;

public sealed class AccountClosedMapper : IIntegrationEventMapper<AccountClosed>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(AccountClosed domainEvent, DomainEventMetadata metadata) =>
        [new AccountClosedIntegrationEvent(domainEvent.AccountId.Value, metadata.EventId, metadata.OccurredAt)];
}
