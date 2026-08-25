namespace GaWeCodes.Thessera.Domain.Events;

public interface IDomainEventRaiser
{
    void Raise(IDomainEvent domainEvent);
}
