using System.Text.Json;

namespace StateStoredWithMessaging;

public static class StateStoredWithMessagingJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}
