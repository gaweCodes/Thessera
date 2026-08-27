using System.Text.Json;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace StateStoredWithMessaging;

public sealed class SentIntegrationEventReporter
{
    public void Report(IIntegrationEvent integrationEvent) =>
        Console.WriteLine($"event sent: {JsonSerializer.Serialize(integrationEvent, StateStoredWithMessagingJson.Options)}");
}
