using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Application.DomainEvents;

public interface IProjectionHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(TDomainEvent domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken);
}
