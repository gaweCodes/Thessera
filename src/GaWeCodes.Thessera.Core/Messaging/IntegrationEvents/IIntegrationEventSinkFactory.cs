using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;

namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

/// <summary>
/// Builds the sink that mapped integration events are handed to, for one message being handled.
/// </summary>
/// <remarks>
/// A factory rather than a sink because the emitter belongs to the session currently handling a
/// message: publishing through it keeps the outgoing events inside that same delivery transaction,
/// instead of sending them and then failing.
/// <para>
/// With no transport configured, the implementation returns the sink that logs a warning per
/// discarded event.
/// </para>
/// </remarks>
public interface IIntegrationEventSinkFactory
{
    /// <summary>
    /// Creates the sink for the message currently being handled.
    /// </summary>
    /// <param name="emitter">The engine's publishing channel for that message.</param>
    /// <returns>A sink that publishes through <paramref name="emitter"/>.</returns>
    IIntegrationEventSink Create(IMessageEmitter emitter);
}
