using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

public sealed class ReconstitutionTests
{
    [Fact]
    public void Reconstitute_ReturnsAnUnidentifiedAggregateWithoutEvents()
    {
        var aggregate = Reconstitute<ReconstitutedAggregate>();

        Assert.True(aggregate.Id.IsEmpty);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Reconstitute_ReturnsADistinctInstanceEachTime()
    {
        var first = Reconstitute<ReconstitutedAggregate>();
        var second = Reconstitute<ReconstitutedAggregate>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Reconstitute_YieldsAHullThatRestoresPersistedState()
    {
        var aggregate = Reconstitute<ReconstitutedAggregate>();
        var persisted = new TestState(new TestId(42), 42);

        ((IStateOwner)aggregate).Restore(persisted);

        Assert.Equal(new TestId(42), aggregate.Id);
        Assert.Equal(42, aggregate.CurrentState.Value);

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Reconstitute_YieldsAHullThatReplaysHistory()
    {
        var aggregate = Reconstitute<ReconstitutableEventSourcedAggregate>();

        ((IEventSourcedAggregateRoot<TestId>)aggregate)
            .LoadFromHistory([new TestDomainEvent(1), new TestDomainEvent(2)]);

        Assert.Equal(new TestId(2), aggregate.Id);
        Assert.Equal(2, ((IStateOwner)aggregate).Version);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void NamedFactory_ProducesAnIdentifiedAggregateThatRecordedItsEvent()
    {
        var aggregate = ReconstitutedAggregate.Create(7);

        Assert.Equal(new TestId(7), aggregate.Id);
        Assert.Single(aggregate.DomainEvents);
    }

    [Fact]
    public void Restore_BringsBackTheChildrenCarriedByTheState()
    {
        var aggregate = Reconstitute<ParentAggregate>();
        var persisted = new ParentState(new TestId(1), 0)
        {
            Version = 5,
            Children = new List<ChildState> { new(new TestId(2), 3) },
        };

        ((IStateOwner)aggregate).Restore(persisted);

        var child = Assert.Single(aggregate.Children);
        Assert.Equal(new TestId(2), child.Id);
        Assert.Equal(3, child.Value);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Restore_YieldsChildViewsThatStayLiveAgainstTheRoot()
    {
        var aggregate = Reconstitute<ParentAggregate>();
        ((IStateOwner)aggregate).Restore(new ParentState(new TestId(1), 0)
        {
            Version = 5,
            Children = new List<ChildState> { new(new TestId(2), 3) },
        });

        aggregate.Child(new TestId(2)).ChangeValue(9);

        Assert.Equal(9, aggregate.Child(new TestId(2)).Value);
        Assert.Equal(6, ((IStateOwner)aggregate).Version);
    }

    private static TAggregate Reconstitute<TAggregate>()
        where TAggregate : class =>
        (TAggregate)Activator.CreateInstance(typeof(TAggregate), nonPublic: true)!;
}
