namespace GaWeCodes.Thessera.Core.Messaging.Transport;

/// <summary>
/// What a transport package hands to <c>UseMessagingTransport</c> to announce itself.
/// </summary>
/// <remarks>
/// Without a transport no integration event leaves the service — the runtime falls back to a sink
/// that logs a warning per discarded event, while domain events and projections keep running
/// untouched.
/// <para>
/// This interface is broker-neutral on purpose. The parts that touch the message engine live in a
/// runtime-specific interface the adapter also implements, and topic routing lives in the core, so
/// <c>[IntegrationEventTopic]</c> works on any transport.
/// </para>
/// </remarks>
/// <seealso cref="MessagingTransportRegistrationContext"/>
public interface IMessagingTransportAdapter
{
    /// <summary>
    /// Gets the name this transport is called by in diagnostics and startup messages.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the bounded context this host publishes as.
    /// </summary>
    /// <value>
    /// A single lower-case kebab-case word. It is the first segment of every routing key the host
    /// publishes under, it is stamped onto outgoing messages so a service can skip its own echo, and
    /// publishing an event whose topic names a different context is refused.
    /// </value>
    string ContextName { get; }

    /// <summary>
    /// Registers everything this transport needs.
    /// </summary>
    /// <param name="context">
    /// The service collection, the host's provisioning choice, what it subscribed to, and the way to
    /// announce the runtime the transport needs.
    /// </param>
    void Register(MessagingTransportRegistrationContext context);
}
