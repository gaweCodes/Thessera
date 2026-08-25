using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Runtime;

namespace GaWeCodes.Thessera.Wolverine.DependencyInjection.Validation;

internal sealed class WolverineRuntimeCheck(
    IServiceProvider serviceProvider,
    IWiringSnapshot wiring) : SynchronousStartupCheck
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        if (!wiring.RequiresRuntime)
        {
            return;
        }

        if (serviceProvider.GetService<IWolverineRuntime>() is null)
        {
            throw new InvalidOperationException(
                "The selected Building Block capabilities (persistence and/or integration-event messaging) require " +
                "Wolverine, but no Wolverine runtime is registered. Register through the host-builder overload â€” " +
                "builder.AddThessera(...) â€” which calls UseWolverine() and applies the Building Block " +
                "configuration itself. A host that deliberately wires Wolverine on top of the IServiceCollection " +
                "overload calls UseWolverine() on the host builder instead.");
        }
    }
}
