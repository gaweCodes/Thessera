using Microsoft.Extensions.Hosting;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;

/// <summary>
/// The message engine a host starts. One per host.
/// </summary>
/// <remarks>
/// This seam is why the composition root has no message engine in its dependency graph: a host that
/// only dispatches commands in process never activates one. A store or transport package announces
/// the runtime it needs, and <see cref="RuntimeActivation.GetOrAdd{TActivator}"/> makes sure two of
/// them share one rather than each getting their own.
/// <para>
/// Because activation needs the host builder, <c>AddThessera</c> has to be called on
/// <see cref="IHostApplicationBuilder"/> whenever a runtime is involved. The
/// <c>IServiceCollection</c> overload cannot activate one.
/// </para>
/// </remarks>
public interface IRuntimeActivator
{
    /// <summary>
    /// Starts the runtime for this host.
    /// </summary>
    /// <param name="builder">The host builder the engine is added to.</param>
    /// <param name="wiring">
    /// What the composition root decided — whether a store was selected, which transport was chosen,
    /// what the host subscribed to, and whether it may provision infrastructure.
    /// </param>
    /// <remarks>
    /// Called once, at the end of <c>AddThessera</c>, after every package has contributed its
    /// registrations.
    /// </remarks>
    void Activate(IHostApplicationBuilder builder, IWiringSnapshot wiring);
}
