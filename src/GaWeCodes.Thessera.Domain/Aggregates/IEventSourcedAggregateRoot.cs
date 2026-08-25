using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

public interface IEventSourcedAggregateRoot<TKey> : IAggregateRoot<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    void LoadFromHistory(IEnumerable<IDomainEvent> history);
}
