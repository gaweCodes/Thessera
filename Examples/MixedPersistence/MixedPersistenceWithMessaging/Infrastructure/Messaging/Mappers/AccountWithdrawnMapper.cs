using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace MixedPersistenceWithMessaging;

public sealed class AccountWithdrawnMapper : IIntegrationEventMapper<AccountWithdrawn>
{
    public IReadOnlyCollection<IIntegrationEvent> Map(AccountWithdrawn domainEvent, DomainEventMetadata metadata) =>
        [new AccountWithdrawnIntegrationEvent(domainEvent.AccountId.Value, domainEvent.Amount, metadata.EventId, metadata.OccurredAt)];
}
