using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace HullFixture;

public readonly record struct LeakyHullId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public sealed record LeakyHullState(LeakyHullId Id) : AggregateState<LeakyHullState, LeakyHullId>
{
    public static LeakyHullState Empty => new(default(LeakyHullId));

    public override LeakyHullState Apply(IDomainEvent domainEvent) => this;
}

[AggregateName("leaky-hull")]
public sealed class LeakyHull : AggregateRoot<LeakyHullId, LeakyHullState>
{
    internal LeakyHull() : base(LeakyHullState.Empty)
    {
    }
}
