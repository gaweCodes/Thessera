using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

public interface IAggregateRoot<TKey> : IEntity<TKey>, IHasDomainEvents
    where TKey : struct, IEntityKey, IEquatable<TKey>;
