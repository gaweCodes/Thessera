using GaWeCodes.Thessera.Core.Persistence;
using HullFixture;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregateFactoryTests
{
    [Fact]
    public void CreateEmpty_ReturnsAnUnidentifiedHullThroughThePrivateConstructor()
    {
        var hull = AggregateFactory.CreateEmpty<Counter>();

        Assert.True(hull.Id.IsEmpty);
        Assert.Empty(hull.DomainEvents);
    }

    [Fact]
    public void CreateEmpty_ReturnsADistinctInstanceEachTime()
    {
        var first = AggregateFactory.CreateEmpty<Counter>();
        var second = AggregateFactory.CreateEmpty<Counter>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddThessera_WithAnUnreconstitutableAggregate_FailsAtRegistration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddThessera(options => options
                .AddDomainEventsFrom(typeof(SealedHull).Assembly)
                .UseEfCoreStateStore<FlushProbeContext>("Host=design-time")));

        Assert.Contains(nameof(SealedHull), exception.Message, StringComparison.Ordinal);
        Assert.Contains("parameterless constructor", exception.Message, StringComparison.Ordinal);
    }
}
