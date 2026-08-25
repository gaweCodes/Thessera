using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Core.Startup;

internal sealed class StartupCheckRunner(IEnumerable<IStartupCheck> checks) : IHostedLifecycleService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        RunAsync(StartupPhase.BeforeHostedServicesStart, cancellationToken);

    public Task StartedAsync(CancellationToken cancellationToken) =>
        RunAsync(StartupPhase.AfterHostedServicesStarted, cancellationToken);

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunAsync(StartupPhase phase, CancellationToken cancellationToken)
    {
        foreach (var check in checks)
        {
            if (check.Phase == phase)
            {
                await check.RunAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

