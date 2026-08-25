using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Need to check if the overwritten equality operator handles null correctly")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Assertions", "xUnit2024:Do not use boolean asserts for simple equality tests", Justification = "Need to check if the overwritten equality operator handles null correctly")]
public sealed class AggregateRootTests
{
    [Fact]
    public void Constructor_WithInitialState_DerivesIdFromState()
    {
        var id = new TestId(7);

        var aggregate = new TestAggregate(id);

        Assert.Equal(id, aggregate.Id);
    }

    [Fact]
    public void NewAggregate_HasNoDomainEvents()
    {
        var aggregate = new TestAggregate(new TestId(1));

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void RaiseEvent_AppliesEventToStateAndRecordsIt()
    {
        var aggregate = new TestAggregate(new TestId(1));

        aggregate.Raise(new TestDomainEvent(5));

        Assert.Equal(new TestId(5), aggregate.Id);
        Assert.Equal(5, aggregate.CurrentState.Value);
        Assert.Single(aggregate.DomainEvents);
    }

    [Fact]
    public void RaiseEvent_Null_ThrowsArgumentNullException()
    {
        var aggregate = new TestAggregate(new TestId(1));

        Assert.Throws<ArgumentNullException>(() => aggregate.Raise(null!));
    }

    [Fact]
    public void RaiseEvent_SurfacesEventsInOrder()
    {
        var aggregate = new TestAggregate(new TestId(1));
        var first = new TestDomainEvent(1);
        var second = new TestDomainEvent(2);

        aggregate.Raise(first);
        aggregate.Raise(second);

        Assert.Equal([first, second], aggregate.DomainEvents);
    }

    [Fact]
    public void RaiseEvent_WhenAppliedStateLeavesIdEmpty_ThrowsDomainValidationException()
    {
        var aggregate = new NeverIdentifiedAggregate();

        var ex = Assert.Throws<DomainValidationException>(
            () => aggregate.Raise(new TestDomainEvent(5)));
        Assert.Equal(
            "The aggregate's identity must be set to a non-empty value by the applied event.",
            ex.Message);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesTheCollection()
    {
        var aggregate = new TestAggregate(new TestId(1));
        aggregate.Raise(new TestDomainEvent(1));

        ((IDomainEventOwner)aggregate).ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Equals_SameTypeSameId_AreEqual()
    {
        var a = new TestAggregate(new TestId(1));
        var b = new TestAggregate(new TestId(1));

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_SameIdDifferentType_AreNotEqual()
    {
        var a = new TestAggregate(new TestId(1));
        var b = new OtherTestAggregate(new TestId(1));

        Assert.False(a.Equals(b as object));
    }

    [Fact]
    public void Equals_DifferentId_AreNotEqual()
    {
        var a = new TestAggregate(new TestId(1));
        var b = new TestAggregate(new TestId(2));

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void EqualityOperator_BothNull_AreEqual()
    {
        TestAggregate? a = null;
        TestAggregate? b = null;

        Assert.True(a == b);
    }

    [Fact]
    public void EqualityOperator_OneNull_AreNotEqual()
    {
        var a = new TestAggregate(new TestId(1));

        Assert.False(a == null);
        Assert.False(null == a);
        Assert.True(a != null);
    }

    [Fact]
    public void Raiser_IsImplementedExplicitlySoDomainCodeCannotSeeIt()
    {
        var aggregate = ParentAggregate.Create(new TestId(1));

        Assert.Null(aggregate.GetType().GetMethod("Raise"));

        ((IDomainEventRaiser)aggregate).Raise(new ChildAdded(new TestId(2), 3));

        Assert.Single(aggregate.Children);
    }

    [Fact]
    public void ClearDomainEvents_AlsoDropsEventsRaisedByAChild()
    {
        var aggregate = ParentAggregate.Create(new TestId(1));
        aggregate.AddChild(new TestId(2), 3);
        aggregate.Child(new TestId(2)).ChangeValue(9);

        ((IDomainEventOwner)aggregate).ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
        Assert.Equal(9, aggregate.Child(new TestId(2)).Value);
    }

    [Fact]
    public void ChildEvents_AreFoldedByTheRootStateWithoutExtraRouting()
    {
        var aggregate = ParentAggregate.Create(new TestId(1));
        aggregate.AddChild(new TestId(2), 3);
        aggregate.AddChild(new TestId(3), 4);

        aggregate.Child(new TestId(3)).ChangeValue(11);

        Assert.Equal([3, 11], aggregate.Children.Select(child => child.Value));
    }

    [Fact]
    public void RaiseEvent_WhenTheStateReturnsNull_NamesTheStateAndTheEventInsteadOfDereferencingIt()
    {
        var aggregate = new NullApplyAggregate();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => aggregate.Raise(new TestDomainEvent(1)));

        Assert.Contains(nameof(NullApplyState), thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestDomainEvent), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("never a state an aggregate can be in", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RaiseEvent_WhenTheStateReturnsItselfForAnUnhandledEvent_IsAccepted()
    {
        var aggregate = new TestEventSourcedAggregate();
        aggregate.Raise(new TestDomainEvent(1));

        aggregate.Raise(new ParentCreated(new TestId(9)));

        Assert.Equal(1, aggregate.CurrentState.Value);
        Assert.Equal(2, ((IStateOwner)aggregate).Version);
    }
}
