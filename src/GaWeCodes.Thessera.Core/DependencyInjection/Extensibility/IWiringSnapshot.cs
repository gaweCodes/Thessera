using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Messaging.Transport;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;

/// <summary>
/// What the composition root decided, handed to a runtime activator so that it can configure itself
/// without reaching back into the options.
/// </summary>
/// <remarks>
/// Read at activation time, after every package has contributed. It is a snapshot rather than a live
/// view precisely so that a runtime cannot change what it is being told about.
/// </remarks>
public interface IWiringSnapshot
{
    /// <summary>
    /// Gets a value indicating whether this host needs a message engine at all.
    /// </summary>
    /// <value>
    /// <see langword="true"/> once persistence or integration-event messaging is selected — both
    /// need durable delivery. A host that only dispatches commands in process needs no runtime.
    /// </value>
    bool RequiresRuntime { get; }

    /// <summary>
    /// Gets a value indicating whether a store was selected.
    /// </summary>
    /// <remarks>
    /// The outbox only exists with a store, so this decides whether the runtime has a message store
    /// to be durable against.
    /// </remarks>
    bool PersistenceSelected { get; }

    /// <summary>
    /// Gets a value indicating whether this host may create schema, exchanges and queues.
    /// </summary>
    bool ProvisionsInfrastructure { get; }

    /// <summary>
    /// Gets the selected transport, or <see langword="null"/> when the host publishes nothing.
    /// </summary>
    IMessagingTransportAdapter? Transport { get; }

    /// <summary>
    /// Gets what the host subscribed to, or <see langword="null"/> when it listens to nothing.
    /// </summary>
    IntegrationEventSubscription? Subscription { get; }

    /// <summary>
    /// Asks the selected store whether a fault is worth retrying.
    /// </summary>
    /// <param name="exception">The exception to judge.</param>
    /// <returns>
    /// <see langword="true"/> when the store calls it transient, so the runtime retries with a
    /// cooldown instead of moving the message to the error queue. Without a store nothing is
    /// transient.
    /// </returns>
    bool IsTransientFault(Exception exception);
}
