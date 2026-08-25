using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Tests;

public sealed class CompositionSingleCallTests
{
    [Fact]
    public void SecondCall_OnTheSameServiceCollection_Throws()
    {
        var services = new ServiceCollection();
        services.AddThessera(_ => { });

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddThessera(_ => { }));

        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondCall_OnTheHostBuilder_Throws()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddThessera(_ => { });

        Assert.Throws<InvalidOperationException>(() => builder.AddThessera(_ => { }));
    }

    [Fact]
    public void SecondCall_DoesNotOverwriteTheFirstRegistrationOfTheSharedState()
    {
        var services = new ServiceCollection();
        services.AddThessera(options => options.AddPipelineBehavior(typeof(StrayBehavior<,>), 500));
        var registrationsBefore = CountStrayBehaviorRegistrations(services);

        Assert.Throws<InvalidOperationException>(() => services.AddThessera(_ => { }));
        var registrationsAfter = CountStrayBehaviorRegistrations(services);
        Assert.Equal(registrationsBefore, registrationsAfter);
        Assert.Equal(1, registrationsAfter);
    }

    [Fact]
    public void BehaviorRegisteredOnTheServiceCollection_FailsRegistration()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(StrayBehavior<,>));

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddThessera(_ => { }));

        Assert.Contains(nameof(StrayBehavior<object, Result>), exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddPipelineBehavior", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BehaviorRegisteredByFactory_FailsRegistration()
    {
        var services = new ServiceCollection();
        services.AddTransient<IPipelineBehavior<ProbeCommand, Result>>(_ => new StrayBehavior<ProbeCommand, Result>());

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddThessera(_ => { }));

        Assert.Contains("factory-registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BehaviorRegisteredThroughOptions_PassesRegistration()
    {
        var services = new ServiceCollection();

        services.AddThessera(options => options.AddPipelineBehavior(typeof(StrayBehavior<,>), 500));
        Assert.Equal(1, CountStrayBehaviorRegistrations(services));
    }

    private static int CountStrayBehaviorRegistrations(IServiceCollection services) =>
        services.Count(descriptor =>
            descriptor.ServiceType == typeof(IPipelineBehavior<,>)
            && descriptor.ImplementationType == typeof(StrayBehavior<,>));

    private sealed record ProbeCommand : ICommand;

    private sealed class StrayBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TResponse : Result
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            RequestPipeline<TResponse> pipeline,
            CancellationToken cancellationToken) => pipeline.NextAsync(cancellationToken);
    }
}
