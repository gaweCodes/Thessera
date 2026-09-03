using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Registration;

internal sealed class PersistenceRegistrar(
    IServiceCollection services,
    PersistenceSelection persistence,
    ProvisioningSelection provisioning,
    RuntimeActivation runtime)
{
    public void UseNone() => persistence.Select(PersistenceChoice.NoPersistence);

    public void Use(IPersistenceAdapter adapter, IReadOnlyCollection<Type>? forAggregates = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        var choice = PersistenceChoice.For(adapter, forAggregates);
        persistence.Select(choice);
        adapter.Register(new PersistenceRegistrationContext(
            services,
            () => provisioning.ProvisionsInfrastructure,
            runtime,
            choice.StoreId,
            choice.ClaimedAggregates));
    }
}
