using GaWeCodes.Thessera.Core.Startup;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Persistence.Marten;

internal sealed class MartenSchemaProvisioner(
    IServiceProvider serviceProvider,
    Func<bool> provisionsInfrastructure) : IStartupCheck
{
    public StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!provisionsInfrastructure())
        {
            return;
        }

        if (serviceProvider.GetService<IDocumentStore>() is not { } store)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync().ConfigureAwait(false);
    }
}
