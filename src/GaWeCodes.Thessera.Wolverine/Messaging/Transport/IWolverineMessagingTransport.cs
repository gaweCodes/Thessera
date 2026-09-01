using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Messaging.Transport;

/// <summary>
/// What a transport package implements so that the runtime can configure its broker: the
/// broker-neutral adapter contract, plus the two calls that touch the message engine directly.
/// </summary>
/// <remarks>
/// A transport selected through <c>UseMessagingTransport</c> that does <em>not</em> implement this
/// interface is refused at startup with an explicit message, rather than starting a host whose
/// integration events are dropped in silence.
/// <para>
/// Topic routing is deliberately not part of this contract. It lives in the broker-neutral core, so
/// <c>[IntegrationEventTopic]</c> takes effect on any transport and a transport author contributes
/// nothing to make it work.
/// </para>
/// </remarks>
public interface IWolverineMessagingTransport : IMessagingTransportAdapter
{
    /// <summary>
    /// Configures the broker connection and the publishing rules.
    /// </summary>
    /// <param name="options">The message engine's options.</param>
    /// <param name="provisionInfrastructure">
    /// Whether this host may create exchanges, queues and bindings. Normally
    /// <see langword="false"/>: a service leaves that to a migration job so that starting a second
    /// instance cannot change the broker.
    /// </param>
    /// <remarks>
    /// Declare the topology either way. Declaring is what lets a startup check verify that it is
    /// actually there; only creating it is gated by
    /// <paramref name="provisionInfrastructure"/>.
    /// </remarks>
    void Configure(WolverineOptions options, bool provisionInfrastructure);

    /// <summary>
    /// Configures the listening side, when the host subscribes to other services' events.
    /// </summary>
    /// <param name="options">The message engine's options.</param>
    /// <param name="subscription">
    /// The endpoint to listen on and the topic patterns to bind to it. At least one non-blank
    /// pattern is guaranteed, because a queue with no binding receives nothing and neither the
    /// broker nor the engine calls that an error.
    /// </param>
    /// <remarks>
    /// Called only when the host declared a subscription. Use a durable inbox: an integration event
    /// that arrives during a restart should still be handled.
    /// </remarks>
    void ConfigureSubscription(WolverineOptions options, IntegrationEventSubscription subscription);
}
