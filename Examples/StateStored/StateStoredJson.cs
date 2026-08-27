using System.Text.Json;

namespace StateStored;

public static class StateStoredJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}
