using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregateStateSelfBindingCheckTests
{
    [Fact]
    public async Task AStateNamingAnotherTypeAsItself_FailsTheStartWithTheReason()
    {
        using var provider = BuildProvider(typeof(AggregateStateSelfBindingCheckTests).Assembly);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAggregateStateSelfBindingCheck(provider));

        Assert.Contains(nameof(MisboundState), thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(WellBoundState), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidCastException", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStateNamingItself_PassesTheStart()
    {
        using var provider = BuildProvider(typeof(AggregateState<,>).Assembly);

        await RunAggregateStateSelfBindingCheck(provider);
    }

    private static ServiceProvider BuildProvider(System.Reflection.Assembly assembly)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddThessera(options => options.AddHandlersFrom(assembly));
        return services.BuildServiceProvider();
    }

    private static async Task RunAggregateStateSelfBindingCheck(ServiceProvider provider)
    {
        var check = provider.GetServices<IStartupCheck>()
            .Single(candidate => candidate.GetType().Name == "AggregateStateSelfBindingCheck");

        await check.RunAsync(TestContext.Current.CancellationToken);
    }

    private readonly record struct SelfBindingProbeId(Guid Value) : IEntityKey<Guid>
    {
        public bool IsEmpty => Value == Guid.Empty;
    }

    private sealed record WellBoundState(SelfBindingProbeId Id)
        : AggregateState<WellBoundState, SelfBindingProbeId>
    {
        public override WellBoundState Apply(IDomainEvent domainEvent) => this;
    }

    private sealed record MisboundState(SelfBindingProbeId Id)
        : AggregateState<WellBoundState, SelfBindingProbeId>
    {
        public override WellBoundState Apply(IDomainEvent domainEvent) => new(Id);
    }
}
