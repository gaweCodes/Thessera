using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

public sealed class TransientIdentityTests
{
    [Fact]
    public void TwoUnidentifiedHulls_CompareEqual()
    {
        var first = Reconstitute<ReconstitutedAggregate>();
        var second = Reconstitute<ReconstitutedAggregate>();

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void AnIdentifiedHull_NoLongerEqualsAnUnidentifiedOne()
    {
        var identified = Reconstitute<ReconstitutedAggregate>();
        var unidentified = Reconstitute<ReconstitutedAggregate>();

        ((IStateOwner)identified).Restore(new TestState(new TestId(1), 1));

        Assert.NotEqual(identified, unidentified);
    }

    [Fact]
    public void Restore_WithAnEmptyIdentity_Throws()
    {
        var aggregate = Reconstitute<ReconstitutedAggregate>();

        var ex = Assert.Throws<DomainValidationException>(
            () => ((IStateOwner)aggregate).Restore(TestState.Empty));

        Assert.Equal("The restored state must carry a non-empty identity.", ex.Message);
    }

    [Fact]
    public void RaiseEvent_ThatLeavesTheIdentityEmpty_Throws()
    {
        var aggregate = new NeverIdentifiedAggregate();

        Assert.Throws<DomainValidationException>(() => aggregate.Raise(new TestDomainEvent(5)));
    }

    [Fact]
    public void AChildEntity_CannotBeConstructedWithoutIdentity()
    {
        var parent = ParentAggregate.Create(new TestId(1));

        Assert.Throws<DomainValidationException>(
            () => new TestChild(parent, TestId.Empty));
    }

    [Fact]
    public void AHullsHashCode_ChangesWhenItGainsIdentity()
    {
        var aggregate = Reconstitute<ReconstitutedAggregate>();
        var before = aggregate.GetHashCode();

        ((IStateOwner)aggregate).Restore(new TestState(new TestId(1), 1));

        Assert.NotEqual(before, aggregate.GetHashCode());
    }

    [Fact]
    public void EveryWayOutOfTheDomain_YieldsAnIdentifiedAggregate()
    {
        var created = ReconstitutedAggregate.Create(7);

        var restored = Reconstitute<ReconstitutedAggregate>();
        ((IStateOwner)restored).Restore(new TestState(new TestId(8), 8));

        var replayed = Reconstitute<ReconstitutableEventSourcedAggregate>();
        ((IEventSourcedAggregateRoot<TestId>)replayed).LoadFromHistory([new TestDomainEvent(9)]);

        Assert.All(
            new EntityBase<TestId>[] { created, restored, replayed },
            aggregate => Assert.False(aggregate.Id.IsEmpty));
    }

    private static TAggregate Reconstitute<TAggregate>()
        where TAggregate : class =>
        (TAggregate)Activator.CreateInstance(typeof(TAggregate), nonPublic: true)!;
}
