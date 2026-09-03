using GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Wolverine.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence.Durability;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class EfCoreOutboxDurability(
    IEfCoreDatabaseDriver driver,
    string writeConnectionString,
    WolverineRuntimeActivator runtime,
    Type contextType)
    : IOutboxDurabilityConfigurator
{
    public void Configure(WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var role = runtime.TryClaimMainMessageStore() ? MessageStoreRole.Main : MessageStoreRole.Ancillary;
        driver.PersistMessages(options, writeConnectionString, role, role == MessageStoreRole.Main ? null : contextType);
        options.UseEntityFrameworkCoreTransactions();
    }
}
