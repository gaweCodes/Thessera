using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Messaging.IntegrationEvents;

public static partial class OwnContextIntegrationEventFilter
{
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
