using System.Text.Json;

namespace MixedPersistence;

public static class MixedPersistenceJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}
