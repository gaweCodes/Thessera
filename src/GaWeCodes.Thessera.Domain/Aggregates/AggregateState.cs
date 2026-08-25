using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

public abstract record AggregateState<TSelf, TKey>
    where TSelf : AggregateState<TSelf, TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    public abstract TKey Id { get; init; }

    public long Version { get; init; }

    public abstract TSelf Apply(IDomainEvent domainEvent);

    internal TSelf WithVersion(long version) => (TSelf)(object)(this with { Version = version });
}
