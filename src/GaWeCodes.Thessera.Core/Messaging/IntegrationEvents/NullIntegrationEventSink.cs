using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using Microsoft.Extensions.Logging;

namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

internal sealed class NullIntegrationEventSink(ILogger<NullIntegrationEventSink> logger) : IIntegrationEventSink
{
    private static readonly Action<ILogger, string, Exception?> EventDiscardedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "IntegrationEventDiscarded"),
            "No messaging transport is configured; discarding integration event {IntegrationEventType}");

    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        EventDiscardedMessage(logger, integrationEvent.GetType().Name, null);
        return Task.CompletedTask;
    }
}

internal sealed class NullIntegrationEventSinkFactory(ILogger<NullIntegrationEventSink> logger) : IIntegrationEventSinkFactory
{
    private readonly NullIntegrationEventSink _sink = new(logger);

    public IIntegrationEventSink Create(IMessageEmitter emitter) => _sink;
}
