using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;

namespace EventSourcedWithMessaging;

public sealed class ConsoleReportingIntegrationEventSinkFactory(string sourceContext, SentIntegrationEventReporter reporter)
    : IIntegrationEventSinkFactory
{
    public IIntegrationEventSink Create(IMessageEmitter emitter) =>
        new ConsoleReportingIntegrationEventSink(sourceContext, reporter, emitter);
}
