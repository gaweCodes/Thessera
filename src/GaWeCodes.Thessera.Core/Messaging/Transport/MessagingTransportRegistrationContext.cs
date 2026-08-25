using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.Messaging.Transport;

public sealed class MessagingTransportRegistrationContext(
    IServiceCollection services,
    Func<bool> provisionsInfrastructure,
    Func<IntegrationEventSubscription?> subscription,
    RuntimeActivation runtime)
{
    public IServiceCollection Services => services;

    public bool ProvisionsInfrastructure => provisionsInfrastructure();

    public IntegrationEventSubscription? Subscription => subscription();

    public TActivator UseRuntime<TActivator>(Func<TActivator> create)
        where TActivator : class, IRuntimeActivator =>
        runtime.GetOrAdd(create);
}
