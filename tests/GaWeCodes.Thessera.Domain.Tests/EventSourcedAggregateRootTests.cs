using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Need to check if the overwritten equality operator handles null correctly")]
public sealed class EventSourcedAggregateRootTests
{
    [Fact]
    public void RaiseEvent_AppliesStateAppendsEventAndIncrementsVersion()
    {
        var aggregate = new TestEventSourcedAggregate();

        aggregate.Raise(new TestDomainEvent(5));

        Assert.Equal(new TestId(5), aggregate.Id);
        Assert.Equal(5, aggregate.CurrentState.Value);
        Assert.Single(aggregate.DomainEvents);
        Assert.Equal(1, ((IStateOwner)aggregate).Version);
    }

    [Fact]
    public void RaiseEvent_WithNonDomainEventImplementation_IsAccepted()
    {
        var aggregate = new TestEventSourcedAggregate();

        aggregate.Raise(new RawDomainEvent(5));

        Assert.IsType<RawDomainEvent>(aggregate.DomainEvents.Single());
    }

    [Fact]
    public void RaiseEvent_MultipleTimes_TracksVersionAndEvents()
    {
        var aggregate = new TestEventSourcedAggregate();

        aggregate.Raise(new TestDomainEvent(1));
        aggregate.Raise(new TestDomainEvent(2));
        aggregate.Raise(new TestDomainEvent(3));

        Assert.Equal(3, aggregate.DomainEvents.Count);
        Assert.Equal(3, ((IStateOwner)aggregate).Version);
        Assert.Equal(3, aggregate.CurrentState.Value);
    }

    [Fact]
    public void LoadFromHistory_ReplaysEventsAndSetsVersion()
    {
        var aggregate = new TestEventSourcedAggregate();
        var history = new IDomainEvent[]
        {
            new TestDomainEvent(1),
            new TestDomainEvent(2),
            new TestDomainEvent(3),
        };

        ((IEventSourcedAggregateRoot<TestId>)aggregate).LoadFromHistory(history);

        Assert.Equal(new TestId(3), aggregate.Id);
        Assert.Equal(3, aggregate.CurrentState.Value);
        Assert.Equal(3, ((IStateOwner)aggregate).Version);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void LoadFromHistory_AfterEventRaised_ThrowsInvalidOperationException()
    {
        var aggregate = new TestEventSourcedAggregate();
        aggregate.Raise(new TestDomainEvent(1));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ((IEventSourcedAggregateRoot<TestId>)aggregate)
                .LoadFromHistory([new TestDomainEvent(2)]));
        Assert.Equal(
            "LoadFromHistory cannot be called after events have been raised on the aggregate.",
            ex.Message);
    }

    [Fact]
    public void LoadFromHistory_ThenRaiseEvent_ContinuesVersioning()
    {
        var aggregate = new TestEventSourcedAggregate();
        ((IEventSourcedAggregateRoot<TestId>)aggregate)
            .LoadFromHistory([new TestDomainEvent(1), new TestDomainEvent(2)]);

        aggregate.Raise(new TestDomainEvent(3));

        Assert.Equal(3, ((IStateOwner)aggregate).Version);
        Assert.Single(aggregate.DomainEvents);
        Assert.Equal(3, aggregate.CurrentState.Value);
    }

    [Fact]
    public void Equals_SameTypeSameId_AreEqual()
    {
        var a = new TestEventSourcedAggregate();
        var b = new TestEventSourcedAggregate();
        a.Raise(new TestDomainEvent(1));
        b.Raise(new TestDomainEvent(1));

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_SameIdDifferentType_AreNotEqual()
    {
        var a = new TestEventSourcedAggregate();
        var b = new OtherEventSourcedAggregate();
        a.Raise(new TestDomainEvent(1));
        b.Raise(new TestDomainEvent(1));

        Assert.False(a.Equals(b as object));
    }

    [Fact]
    public void Equals_DifferentId_AreNotEqual()
    {
        var a = new TestEventSourcedAggregate();
        var b = new TestEventSourcedAggregate();
        a.Raise(new TestDomainEvent(1));
        b.Raise(new TestDomainEvent(2));

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void EqualityOperator_BothNull_AreEqual()
    {
        TestEventSourcedAggregate? a = null;
        TestEventSourcedAggregate? b = null;

        Assert.True(a == b);
    }

    [Fact]
    public void LoadFromHistory_RebuildsChildrenWithoutRecordingEvents()
    {
        var aggregate = (EventSourcedParent)Activator.CreateInstance(
            typeof(EventSourcedParent), nonPublic: true)!;

        ((IEventSourcedAggregateRoot<TestId>)aggregate).LoadFromHistory(
        [
            new ParentCreated(new TestId(1)),
            new ChildAdded(new TestId(2), 3),
            new ChildValueChanged(new TestId(2), 9),
        ]);

        Assert.Equal(9, aggregate.Child(new TestId(2)).Value);
        Assert.Equal(3, ((IStateOwner)aggregate).Version);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void LoadFromHistory_AfterAChildRaisedAnEvent_IsRejected()
    {
        var aggregate = EventSourcedParent.Create(new TestId(1));
        aggregate.AddChild(new TestId(2), 3);
        aggregate.Child(new TestId(2)).ChangeValue(9);

        Assert.Throws<InvalidOperationException>(
            () => ((IEventSourcedAggregateRoot<TestId>)aggregate)
                .LoadFromHistory([new ParentCreated(new TestId(1))]));
    }

    [Fact]
    public void LoadFromHistory_CalledTwice_IsRejectedInsteadOfDuplicatingTheState()
    {
        var aggregate = (EventSourcedParent)Activator.CreateInstance(
            typeof(EventSourcedParent), nonPublic: true)!;
        var loadable = (IEventSourcedAggregateRoot<TestId>)aggregate;
        IDomainEvent[] history = [new ParentCreated(new TestId(1)), new ChildAdded(new TestId(2), 3)];

        loadable.LoadFromHistory(history);

        var thrown = Assert.Throws<InvalidOperationException>(() => loadable.LoadFromHistory(history));

        Assert.Contains("cannot be called twice", thrown.Message, StringComparison.Ordinal);
        Assert.Single(aggregate.Children);
        Assert.Equal(2, ((IStateOwner)aggregate).Version);
    }

    [Fact]
    public void LoadFromHistory_CalledTwiceWithNoHistoryAtAll_IsAlsoRejected()
    {
        var aggregate = (EventSourcedParent)Activator.CreateInstance(
            typeof(EventSourcedParent), nonPublic: true)!;
        var loadable = (IEventSourcedAggregateRoot<TestId>)aggregate;

        loadable.LoadFromHistory([]);

        Assert.Throws<InvalidOperationException>(() => loadable.LoadFromHistory([]));
    }

    [Fact]
    public void LoadFromHistory_WhenTheStateReturnsNull_NamesTheStateAndTheEvent()
    {
        var aggregate = new NullApplyEventSourcedAggregate();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => ((IEventSourcedAggregateRoot<TestId>)aggregate)
                .LoadFromHistory([new TestDomainEvent(1)]));

        Assert.Contains(nameof(NullApplyState), thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestDomainEvent), thrown.Message, StringComparison.Ordinal);
    }
}
