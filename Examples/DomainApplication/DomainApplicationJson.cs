using System.Text.Json;

namespace DomainApplication;

public static class DomainApplicationJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}
