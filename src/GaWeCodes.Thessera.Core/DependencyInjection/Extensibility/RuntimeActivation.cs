namespace GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;

/// <summary>
/// Holds the one runtime a host activates, and makes sure it stays one.
/// </summary>
/// <remarks>
/// A store and a transport that both want the same message engine each ask for it, and both get the
/// same instance back — so the store can register its outbox durability on the very object the
/// transport will configure its endpoints on.
/// </remarks>
public sealed class RuntimeActivation
{
    /// <summary>
    /// Gets the runtime this host will activate, or <see langword="null"/> when nothing has asked
    /// for one.
    /// </summary>
    public IRuntimeActivator? Activator { get; private set; }

    /// <summary>
    /// Returns the host's runtime, creating it on the first call.
    /// </summary>
    /// <typeparam name="TActivator">The runtime being asked for.</typeparam>
    /// <param name="create">Creates the runtime, called only if there is none yet.</param>
    /// <returns>
    /// The host's runtime — the newly created one, or the existing one when something already asked
    /// for the same kind.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="create"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A <em>different</em> runtime is already selected. A host runs exactly one, because that
    /// runtime owns the outbox, the inbox and the local queues every domain event travels through;
    /// two of them would each hold half of the delivery guarantees.
    /// </exception>
    public TActivator GetOrAdd<TActivator>(Func<TActivator> create)
        where TActivator : class, IRuntimeActivator
    {
        ArgumentNullException.ThrowIfNull(create);

        if (Activator is null)
        {
            var activator = create();
            Activator = activator;
            return activator;
        }

        return Activator as TActivator ?? throw new InvalidOperationException(
            $"Two different runtimes were selected for the same host ({Activator.GetType().Name} and " +
            $"{typeof(TActivator).Name}). A host runs exactly one messaging runtime, because that runtime owns the " +
            "outbox, the inbox and the local queues every domain event travels through. Two of them would each hold " +
            "half of the delivery guarantees. Choose one runtime for the whole host.");
    }
}
