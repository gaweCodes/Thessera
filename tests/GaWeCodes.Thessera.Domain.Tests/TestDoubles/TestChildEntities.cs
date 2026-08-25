using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Tests.TestDoubles;

internal sealed record ParentCreated(TestId ParentId) : DomainEvent;

internal sealed record ChildAdded(TestId ChildId, int Value) : DomainEvent;

internal sealed record ChildValueChanged(TestId ChildId, int Value) : DomainEvent;

internal sealed record ChildRemoved(TestId ChildId) : DomainEvent;

internal sealed record ChildState(TestId Id, int Value) : EntityState<ChildState, TestId>
{
    public override ChildState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ChildValueChanged changed => this with { Value = changed.Value },
        _ => this,
    };
}

internal sealed record ParentState(TestId Id, int Value) : AggregateState<ParentState, TestId>
{
    public IReadOnlyCollection<ChildState> Children { get; init; } = new List<ChildState>();

    public static ParentState Empty => new(TestId.Empty, 0);

    public override ParentState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ParentCreated created => this with { Id = created.ParentId },
        ChildAdded added => this with
        {
            Children = Children.Append(new ChildState(added.ChildId, added.Value)).ToList(),
        },
        ChildValueChanged changed => this with
        {
            Children = Children
                .Select(child => child.Id == changed.ChildId ? child.Apply(changed) : child)
                .ToList(),
        },
        ChildRemoved removed => this with
        {
            Children = Children.Where(child => child.Id != removed.ChildId).ToList(),
        },
        _ => this,
    };
}

internal sealed class TestChild : Entity<TestId, ChildState>
{
    internal TestChild(IChildOwner<TestId, ChildState> owner, TestId id)
        : base(owner, id)
    {
    }

    public int Value => GetCurrentState().Value;

    public void ChangeValue(int value) => RaiseEvent(new ChildValueChanged(Id, value));

    public void RaiseNothing() => RaiseEvent(null!);
}

internal sealed class ParentAggregate : AggregateRoot<TestId, ParentState>,
    IChildOwner<TestId, ChildState>
{
    private ParentAggregate() : base(ParentState.Empty)
    {
    }

    public IReadOnlyCollection<TestChild> Children =>
        State.Children.Select(child => Child(child.Id)).ToList();

    public static ParentAggregate Create(TestId id)
    {
        var parent = new ParentAggregate();
        parent.RaiseEvent(new ParentCreated(id));
        return parent;
    }

    public TestChild Child(TestId childId) => new(this, childId);

    public void AddChild(TestId childId, int value) => RaiseEvent(new ChildAdded(childId, value));

    public void RemoveChild(TestId childId) => RaiseEvent(new ChildRemoved(childId));

    ChildState? IChildOwner<TestId, ChildState>.FindChild(TestId childId) =>
        State.Children.FirstOrDefault(child => child.Id == childId);
}

internal sealed class EventSourcedParent : EventSourcedAggregateRoot<TestId, ParentState>,
    IChildOwner<TestId, ChildState>
{
    private EventSourcedParent() : base(ParentState.Empty)
    {
    }

    public IReadOnlyCollection<TestChild> Children =>
        State.Children.Select(child => Child(child.Id)).ToList();

    public static EventSourcedParent Create(TestId id)
    {
        var parent = new EventSourcedParent();
        parent.RaiseEvent(new ParentCreated(id));
        return parent;
    }

    public TestChild Child(TestId childId) => new(this, childId);

    public void AddChild(TestId childId, int value) => RaiseEvent(new ChildAdded(childId, value));

    ChildState? IChildOwner<TestId, ChildState>.FindChild(TestId childId) =>
        State.Children.FirstOrDefault(child => child.Id == childId);
}
