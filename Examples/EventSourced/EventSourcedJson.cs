using System.Text.Json;

namespace EventSourced;

public static class EventSourcedJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}
