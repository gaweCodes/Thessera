using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// What a store adapter is handed when it registers itself.
/// </summary>
/// <param name="services">The service collection being built.</param>
/// <param name="provisionsInfrastructure">Reads the host's provisioning choice when asked.</param>
/// <param name="runtime">Holds the one runtime this host activates.</param>
/// <remarks>
/// The provisioning choice is read lazily on purpose: a host may call
/// <c>ProvisionInfrastructure(...)</c> after selecting the store, and the adapter would otherwise
/// capture whatever happened to be set at the moment it registered.
/// </remarks>
public sealed class PersistenceRegistrationContext(
    IServiceCollection services,
    Func<bool> provisionsInfrastructure,
    RuntimeActivation runtime)
{
    /// <summary>
    /// Gets the service collection to register the store's services into.
    /// </summary>
    public IServiceCollection Services => services;

    /// <summary>
    /// Gets a value indicating whether this host may create schema on its own.
    /// </summary>
    /// <value>
    /// Read at the moment you ask, so it reflects the host's final choice rather than the order the
    /// calls happened to be written in.
    /// </value>
    public bool ProvisionsInfrastructure => provisionsInfrastructure();

    /// <summary>
    /// Announces the runtime this store needs, and returns it so the store can configure it.
    /// </summary>
    /// <typeparam name="TActivator">The runtime being asked for.</typeparam>
    /// <param name="create">Creates the runtime, called only if there is none yet.</param>
    /// <returns>The host's runtime, shared with anything else that asked for the same kind.</returns>
    /// <exception cref="InvalidOperationException">
    /// A different runtime is already selected for this host.
    /// </exception>
    public TActivator UseRuntime<TActivator>(Func<TActivator> create)
        where TActivator : class, IRuntimeActivator =>
        runtime.GetOrAdd(create);
}
