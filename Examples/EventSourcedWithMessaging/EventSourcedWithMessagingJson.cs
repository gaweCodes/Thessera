using System.Text.Json;

namespace EventSourcedWithMessaging;

public static class EventSourcedWithMessagingJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}
