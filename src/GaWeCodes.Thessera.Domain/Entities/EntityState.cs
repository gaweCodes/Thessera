using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Entities;

public abstract record EntityState<TSelf, TKey>
    where TSelf : EntityState<TSelf, TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    public abstract TKey Id { get; init; }

    public abstract TSelf Apply(IDomainEvent domainEvent);
}
