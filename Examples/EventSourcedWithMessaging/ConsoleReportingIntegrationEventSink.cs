using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;

namespace EventSourcedWithMessaging;

public sealed class ConsoleReportingIntegrationEventSink(
    string sourceContext,
    SentIntegrationEventReporter reporter,
    IMessageEmitter emitter) : IIntegrationEventSink
{
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await emitter.PublishAsync(
            integrationEvent,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [IntegrationEventSourceContext.HeaderName] = sourceContext,
            },
            cancellationToken).ConfigureAwait(false);

        reporter.Report(integrationEvent);
    }
}
