using GaWeCodes.Thessera.Tests.TestDoubles;

namespace GaWeCodes.Thessera.Tests;

public sealed class EntityStateTests
{
    [Fact]
    public void Apply_FoldsTheEventIntoANewInstance()
    {
        var state = new ChildState(new TestId(1), 3);

        var applied = state.Apply(new ChildValueChanged(new TestId(1), 7));

        Assert.Equal(7, applied.Value);
        Assert.Equal(3, state.Value);
        Assert.NotSame(state, applied);
    }

    [Fact]
    public void Apply_KeepsTheIdentity()
    {
        var state = new ChildState(new TestId(1), 3);

        var applied = state.Apply(new ChildValueChanged(new TestId(1), 7));

        Assert.Equal(new TestId(1), applied.Id);
    }

    [Fact]
    public void Apply_WithAnUnrelatedEvent_ReturnsTheSameInstance()
    {
        var state = new ChildState(new TestId(1), 3);

        Assert.Same(state, state.Apply(new IgnoredDomainEvent()));
    }

    [Fact]
    public void EntityState_CarriesNoVersion()
    {
        Assert.Null(typeof(ChildState).GetProperty("Version"));
    }
}
