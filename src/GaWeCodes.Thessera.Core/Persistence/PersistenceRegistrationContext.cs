using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.Persistence;

public sealed class PersistenceRegistrationContext(
    IServiceCollection services,
    Func<bool> provisionsInfrastructure,
    RuntimeActivation runtime)
{
    public IServiceCollection Services => services;

    public bool ProvisionsInfrastructure => provisionsInfrastructure();

    public TActivator UseRuntime<TActivator>(Func<TActivator> create)
        where TActivator : class, IRuntimeActivator =>
        runtime.GetOrAdd(create);
}
