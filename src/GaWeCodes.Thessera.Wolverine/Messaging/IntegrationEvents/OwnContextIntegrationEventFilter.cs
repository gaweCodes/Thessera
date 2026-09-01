using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Messaging.IntegrationEvents;

/// <summary>
/// Stops a service consuming the integration events it published itself.
/// </summary>
/// <remarks>
/// Without this a service that binds a broad pattern would hear its own echo — and, if it maps that
/// back into work, do the work twice. Recognition is by the publishing context on the message
/// header, not by the queue or the topic, so a service can subscribe to a pattern that includes its
/// own events and still ignore them.
/// </remarks>
public static partial class OwnContextIntegrationEventFilter
{
    /// <summary>
    /// Decides whether an incoming message should be handled or dropped.
    /// </summary>
    /// <param name="envelope">The incoming message and its headers.</param>
    /// <param name="source">This host's publishing context.</param>
    /// <param name="logger">Records the events that were dropped, at debug level.</param>
    /// <returns>
    /// Stop when the message carries this host's own context, so the handler never runs; continue
    /// otherwise — including when the header is missing, because a message from an unknown source is
    /// not this service's echo.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="envelope"/>, <paramref name="source"/> or <paramref name="logger"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static HandlerContinuation Before(
        Envelope envelope,
        IntegrationEventSourceContext source,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(logger);

        if (!envelope.Headers.TryGetValue(IntegrationEventSourceContext.HeaderName, out var sourceContext)
            || !string.Equals(sourceContext, source.Name, StringComparison.Ordinal))
        {
            return HandlerContinuation.Continue;
        }

        LogDiscardedOwnEvent(logger, envelope.Message?.GetType().FullName ?? "unknown", source.Name);

        return HandlerContinuation.Stop;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Discarded the integration event {MessageType} because this context ({ContextName}) published it itself.")]
    private static partial void LogDiscardedOwnEvent(ILogger logger, string messageType, string contextName);
}
