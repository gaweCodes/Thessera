using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace HullFixture;

public readonly record struct SealedHullId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

[EventName("sealed-hull-created-v1")]
public sealed record SealedHullCreated(SealedHullId HullId) : DomainEvent;

public sealed record SealedHullState(SealedHullId Id) : AggregateState<SealedHullState, SealedHullId>
{
    public static SealedHullState Empty => new(default(SealedHullId));

    public override SealedHullState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        SealedHullCreated created => this with { Id = created.HullId },
        _ => this,
    };
}

[AggregateName("sealed-hull")]
public sealed class SealedHull : AggregateRoot<SealedHullId, SealedHullState>
{
    private SealedHull(SealedHullState state) : base(state)
    {
    }

    public static SealedHull Create(SealedHullId id)
    {
        var hull = new SealedHull(SealedHullState.Empty);
        hull.RaiseEvent(new SealedHullCreated(id));
        return hull;
    }
}
