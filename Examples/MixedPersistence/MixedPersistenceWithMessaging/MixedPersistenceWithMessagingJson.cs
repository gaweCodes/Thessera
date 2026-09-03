using System.Text.Json;

namespace MixedPersistenceWithMessaging;

public static class MixedPersistenceWithMessagingJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}
