using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Wolverine.Persistence;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;

/// <summary>
/// The Wolverine runtime as the composition root sees it: it collects what the store and the host
/// contributed while the container was being built, and applies it when the host starts the engine.
/// </summary>
/// <remarks>
/// One per host, reached through <c>UseWolverineRuntime()</c> rather than constructed directly.
/// Because activation needs the host builder, <c>AddThessera</c> has to be called on
/// <see cref="IHostApplicationBuilder"/> whenever a runtime is involved — the
/// <c>IServiceCollection</c> overload cannot activate one, and a startup check says so instead of
/// letting the host run without an engine.
/// </remarks>
public sealed class WolverineRuntimeActivator : IRuntimeActivator
{
    private readonly List<IOutboxDurabilityConfigurator> _outboxDurability = [];
    private readonly List<Action<WolverineOptions>> _customizations = [];
    private bool _mainMessageStoreClaimed;

    /// <summary>
    /// Registers how a store binds the outbox to its database.
    /// </summary>
    /// <param name="configurator">The store's outbox durability.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configurator"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Called by a store adapter from its <c>Register</c> method. Without it the engine has no
    /// message store, so nothing is durable and a crash between commit and delivery loses the
    /// events.
    /// </remarks>
    public void AddOutboxDurability(IOutboxDurabilityConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        _outboxDurability.Add(configurator);
    }

    /// <summary>
    /// Claims this host's single Wolverine "Main" message store slot, if it is still free.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the caller is the first to ask and now owns the Main slot;
    /// <see langword="false"/> if another store already claimed it, so the caller must register
    /// itself as Wolverine's Ancillary message store instead.
    /// </returns>
    /// <remarks>
    /// Wolverine allows exactly one message store tagged Main per host; every other one must be
    /// Ancillary. A host with a single persistence adapter is unaffected — that adapter is always
    /// the first (and only) caller, so it always claims Main, exactly as before this method existed.
    /// It only matters once two adapters share a host, per ADR 0016: whichever store's outbox
    /// durability is configured first — Marten always eagerly, from its own <c>Register</c>, because
    /// Wolverine's plain <c>AddMarten().IntegrateWithWolverine()</c> offers no ancillary option;
    /// every other store's outbox durability configurator is deferred and runs later, from
    /// <see cref="Activate"/> — becomes Main, and the rest fall back to Ancillary.
    /// </remarks>
    public bool TryClaimMainMessageStore()
    {
        if (_mainMessageStoreClaimed)
        {
            return false;
        }

        _mainMessageStoreClaimed = true;
        return true;
    }

    /// <summary>
    /// Registers configuration of your own, applied after everything the family configures.
    /// </summary>
    /// <param name="customize">The configuration to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="customize"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Reached from a host through <c>CustomizeWolverine</c>. Several customizations are applied in
    /// the order they were added, all of them after the outbox durability.
    /// </remarks>
    public void Customize(Action<WolverineOptions> customize)
    {
        ArgumentNullException.ThrowIfNull(customize);

        _customizations.Add(customize);
    }

    /// <summary>
    /// Starts the message engine for this host, applying the outbox durability first and the host's
    /// own customizations last.
    /// </summary>
    /// <param name="builder">The host builder the engine is added to.</param>
    /// <param name="wiring">What the composition root decided: store, transport, subscription, provisioning.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="wiring"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Called by the composition root, once, at the end of <c>AddThessera</c>. Customizations run
    /// last on purpose, so a host can override what the family set up.
    /// </remarks>
    public void Activate(IHostApplicationBuilder builder, IWiringSnapshot wiring)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(wiring);

        builder.UseWolverine(options =>
        {
            foreach (var durability in _outboxDurability)
            {
                durability.Configure(options);
            }

            foreach (var customize in _customizations)
            {
                customize(options);
            }
        });
    }
}
