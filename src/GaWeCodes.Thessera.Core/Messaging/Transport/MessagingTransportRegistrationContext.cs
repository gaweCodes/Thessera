using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.Messaging.Transport;

/// <summary>
/// What a transport adapter is handed when it registers itself.
/// </summary>
/// <param name="services">The service collection being built.</param>
/// <param name="provisionsInfrastructure">Reads the host's provisioning choice when asked.</param>
/// <param name="subscription">Reads what the host subscribed to when asked.</param>
/// <param name="runtime">Holds the one runtime this host activates.</param>
/// <remarks>
/// Both the provisioning choice and the subscription are read lazily, because a host may declare
/// them after selecting the transport — an adapter that captured them at registration time would
/// silently depend on the order the calls were written in.
/// </remarks>
public sealed class MessagingTransportRegistrationContext(
    IServiceCollection services,
    Func<bool> provisionsInfrastructure,
    Func<IntegrationEventSubscription?> subscription,
    RuntimeActivation runtime)
{
    /// <summary>
    /// Gets the service collection to register the transport's services into.
    /// </summary>
    public IServiceCollection Services => services;

    /// <summary>
    /// Gets a value indicating whether this host may create exchanges and queues on its own.
    /// </summary>
    /// <value>
    /// Read at the moment you ask. Declare the topology either way; only creating it is gated by
    /// this, so that a startup check can still verify what is supposed to be there.
    /// </value>
    public bool ProvisionsInfrastructure => provisionsInfrastructure();

    /// <summary>
    /// Gets what the host subscribed to, or <see langword="null"/> when it listens to nothing.
    /// </summary>
    public IntegrationEventSubscription? Subscription => subscription();

    /// <summary>
    /// Announces the runtime this transport needs, and returns it so the transport can configure it.
    /// </summary>
    /// <typeparam name="TActivator">The runtime being asked for.</typeparam>
    /// <param name="create">Creates the runtime, called only if there is none yet.</param>
    /// <returns>The host's runtime, shared with the store when it asked for the same kind.</returns>
    /// <exception cref="InvalidOperationException">
    /// A different runtime is already selected for this host.
    /// </exception>
    public TActivator UseRuntime<TActivator>(Func<TActivator> create)
        where TActivator : class, IRuntimeActivator =>
        runtime.GetOrAdd(create);
}
