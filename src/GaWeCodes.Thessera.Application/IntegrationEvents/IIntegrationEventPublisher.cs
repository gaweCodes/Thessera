using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Application.IntegrationEvents;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, DomainEventMetadata metadata, IIntegrationEventSink integrationEventSink, CancellationToken cancellationToken);
}
