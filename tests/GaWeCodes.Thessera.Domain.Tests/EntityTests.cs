using GaWeCodes.Thessera.Domain.Rules;
using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Need to check if the overwritten equality operator handles null correctly")]
public sealed class EntityTests
{
    [Fact]
    public void Constructor_WithEmptyId_ThrowsDomainValidationException()
    {
        var ex = Assert.Throws<DomainValidationException>(() => new TestEntity(TestId.Empty));
        Assert.Equal("The id of an entity cannot be empty.", ex.Message);
    }

    [Fact]
    public void Constructor_WithValidId_SetsId()
    {
        var id = new TestId(42);

        var entity = new TestEntity(id);

        Assert.Equal(id, entity.Id);
    }

    [Fact]
    public void Equals_SameTypeSameId_AreEqual()
    {
        var a = new TestEntity(new TestId(1));
        var b = new TestEntity(new TestId(1));

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_SameIdDifferentType_AreNotEqual()
    {
        var a = new TestEntity(new TestId(1));
        var b = new OtherTestEntity(new TestId(1));

        Assert.False(a.Equals(b as object));
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentId_AreNotEqual()
    {
        var a = new TestEntity(new TestId(1));
        var b = new TestEntity(new TestId(2));

        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var a = new TestEntity(new TestId(1));

        Assert.False(a.Equals(null));
        Assert.False(a.Equals((object?)null));
    }

    [Fact]
    public void Equals_WithNonEntityObject_ReturnsFalse()
    {
        var a = new TestEntity(new TestId(1));

        Assert.False(a.Equals("not an entity"));
    }

    [Fact]
    public void EqualityOperator_BothNull_AreEqual()
    {
        TestEntity? a = null;
        TestEntity? b = null;

        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void EqualityOperator_OneNull_AreNotEqual()
    {
        var a = new TestEntity(new TestId(1));
        TestEntity? b = null;

        Assert.False(a == b);
        Assert.False(b == a);
        Assert.True(a != b);
    }

    [Fact]
    public void EqualityOperator_SameReference_AreEqual()
    {
        var a = new TestEntity(new TestId(1));
        var b = a;

        Assert.True(a == b);
    }

    [Fact]
    public void Child_RaisesThroughTheRootAndTheEventLandsInTheRootsList()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);

        parent.Child(new TestId(2)).ChangeValue(9);

        var raised = Assert.IsType<ChildValueChanged>(parent.DomainEvents.Last());
        Assert.Equal(new TestId(2), raised.ChildId);
        Assert.Equal(9, raised.Value);
        Assert.Equal(3, parent.DomainEvents.Count);
    }

    [Fact]
    public void Child_RaisingAnEvent_FoldsItIntoTheRootState()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);
        parent.AddChild(new TestId(3), 4);

        parent.Child(new TestId(2)).ChangeValue(9);

        Assert.Equal(9, parent.Child(new TestId(2)).Value);
        Assert.Equal(4, parent.Child(new TestId(3)).Value);
    }

    [Fact]
    public void Children_KeepTheOrderOfRootAndChildEvents()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);
        parent.Child(new TestId(2)).ChangeValue(9);
        parent.RemoveChild(new TestId(2));

        Assert.Collection(
            parent.DomainEvents,
            first => Assert.IsType<ParentCreated>(first),
            second => Assert.IsType<ChildAdded>(second),
            third => Assert.IsType<ChildValueChanged>(third),
            fourth => Assert.IsType<ChildRemoved>(fourth));
    }

    [Fact]
    public void Child_ReadsThroughTheRootInsteadOfKeepingACopy()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);
        var child = parent.Child(new TestId(2));

        parent.Child(new TestId(2)).ChangeValue(9);

        Assert.Equal(9, child.Value);
    }

    [Fact]
    public void Child_OfARemovedEntity_ThrowsWhenItsStateIsRead()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);
        var child = parent.Child(new TestId(2));
        parent.RemoveChild(new TestId(2));

        var ex = Assert.Throws<DomainValidationException>(() => child.Value);
        Assert.Contains("no longer part of 'ParentAggregate'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Child_WithoutAnOwner_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TestChild(null!, new TestId(2)));
    }

    [Fact]
    public void Child_ReadsStateAndRaisesEventsThroughTheSameOwner()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);

        var child = parent.Child(new TestId(2));
        child.ChangeValue(42);

        Assert.Equal(42, child.Value);
        Assert.Equal(42, parent.Child(new TestId(2)).Value);
    }

    [Fact]
    public void Child_WithAnEmptyId_IsRejected()
    {
        var parent = ParentAggregate.Create(new TestId(1));

        Assert.Throws<DomainValidationException>(
            () => new TestChild(parent, TestId.Empty));
    }

    [Fact]
    public void Child_RaisingNull_IsRejected()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);

        Assert.Throws<ArgumentNullException>(() => parent.Child(new TestId(2)).RaiseNothing());
    }

    [Fact]
    public void Children_AreEqualWhenTheyShareTypeAndIdentity()
    {
        var parent = ParentAggregate.Create(new TestId(1));
        parent.AddChild(new TestId(2), 3);

        Assert.Equal(parent.Child(new TestId(2)), parent.Child(new TestId(2)));
        Assert.NotSame(parent.Child(new TestId(2)), parent.Child(new TestId(2)));
    }
}
