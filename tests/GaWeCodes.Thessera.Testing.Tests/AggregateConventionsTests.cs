using GaWeCodes.Thessera.Testing;
using HullFixture;
using SchemaFixture;
using SchemaGapFixture;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregateConventionsTests
{
    [Fact]
    public void AnAssemblyThatFollowsTheConventions_Passes() =>
        AggregateConventions.Verify([typeof(SampleCreated).Assembly]);

    [Fact]
    public void ADomainEventWithoutAnEventName_IsReported()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => AggregateConventions.Verify([typeof(UnnamedEvent).Assembly]));

        Assert.Contains(nameof(UnnamedEvent), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("[EventName]", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAggregateWithoutAParameterlessConstructor_IsReported()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => AggregateConventions.Verify([typeof(SealedHull).Assembly]));

        Assert.Contains(nameof(SealedHull), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("parameterless constructor", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAssemblyWithNoAggregateAndNoDomainEvent_IsReportedInsteadOfPassingVacuously()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => AggregateConventions.Verify([typeof(string).Assembly]));

        Assert.Contains("vacuous", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryViolation_IsReportedInOneRun_NotJustTheFirst()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => AggregateConventions.Verify([typeof(SealedHull).Assembly, typeof(UnnamedEvent).Assembly]));

        Assert.Contains(nameof(SealedHull), thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(UnnamedEvent), thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithoutAssemblies_Throws() =>
        Assert.Throws<ArgumentNullException>(() => AggregateConventions.Verify(null!));
}
