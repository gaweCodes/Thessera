using System.Text.Json;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace EventSourcedWithMessaging;

public sealed class SentIntegrationEventReporter
{
    public void Report(IIntegrationEvent integrationEvent) =>
        Console.WriteLine($"Event sent: {JsonSerializer.Serialize(integrationEvent, EventSourcedWithMessagingJson.Options)}");
}
