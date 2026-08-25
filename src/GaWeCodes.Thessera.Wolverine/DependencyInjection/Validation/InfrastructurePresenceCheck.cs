using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Persistence.Durability;

namespace GaWeCodes.Thessera.Wolverine.DependencyInjection.Validation;

internal sealed class InfrastructurePresenceCheck(
    IServiceProvider serviceProvider,
    IWiringSnapshot wiring) : IStartupCheck
{
    public StartupPhase Phase => StartupPhase.AfterHostedServicesStarted;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (wiring.ProvisionsInfrastructure || !wiring.PersistenceSelected)
        {
            return;
        }

        if (serviceProvider.GetService<IMessageStore>() is not { } messageStore)
        {
            return;
        }

        try
        {
            await messageStore.Admin.AssertStorageExistsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "This host does not provision infrastructure, but Wolverine's message storage is missing or does " +
                $"not match the configured schema in '{messageStore.Name}'. The outbox is what makes a commit and " +
                "its integration events one unit, so without those tables this host would accept " +
                "commands and lose every event they produce. Start the host that selects " +
                "ProvisionInfrastructure(InfrastructureProvisioning.AtStartup) for this context ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â and let it " +
                "finish ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â before starting this one.",
                exception);
        }
    }
}
