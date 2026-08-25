using GaWeCodes.Thessera.Core.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class WriteDbContextLifetimeCheck<TContext>(IServiceCollection services) : SynchronousStartupCheck
    where TContext : DbContext
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        var lifetime = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(TContext))
            ?.Lifetime;

        if (lifetime is null or ServiceLifetime.Scoped)
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{typeof(TContext).Name}' is registered as {lifetime} and must be registered as Scoped. "
            + "A repository reaches the write context directly, while the commit reaches it through the outbox; "
            + "both resolve it from the current scope, so only a scoped registration makes them the same "
            + "instance. "
            + (lifetime == ServiceLifetime.Transient
                ? "As Transient they are two instances: the repository adds the aggregate to one and the commit "
                + "saves the other, which reports success and writes no row at all."
                : "As Singleton every request shares one change tracker that is never cleared, and concurrent "
                + "requests use it without any thread safety.")
            + $" Remove the AddDbContext<{typeof(TContext).Name}> call and let "
            + $"UseEfCoreStateStore<{typeof(TContext).Name}> register it, or register it as Scoped.");
    }
}
