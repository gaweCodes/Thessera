using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Application.IntegrationEvents;

public interface IIntegrationEventMapper<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    IReadOnlyCollection<IIntegrationEvent> Map(TDomainEvent domainEvent, DomainEventMetadata metadata);
}
