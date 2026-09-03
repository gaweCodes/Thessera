using System.Text.Json;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace MixedPersistenceWithMessaging;

public sealed class SentIntegrationEventReporter
{
    public void Report(IIntegrationEvent integrationEvent) =>
        Console.WriteLine($"event sent: {JsonSerializer.Serialize(integrationEvent, MixedPersistenceWithMessagingJson.Options)}");
}
