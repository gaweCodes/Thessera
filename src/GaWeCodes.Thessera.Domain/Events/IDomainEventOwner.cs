namespace GaWeCodes.Thessera.Domain.Events;

public interface IDomainEventOwner : IHasDomainEvents
{
    void ClearDomainEvents();
}
