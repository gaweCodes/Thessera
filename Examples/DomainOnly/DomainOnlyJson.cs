using GaWeCodes.Thessera.Domain.Events;
using System.Text.Json;

namespace DomainOnly;

public static class DomainOnlyJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };

    public static IReadOnlyList<JsonElement> ToJsonElements(IEnumerable<IDomainEvent> domainEvents) =>
        [.. domainEvents.Select(domainEvent => JsonSerializer.SerializeToElement(domainEvent, domainEvent.GetType(), Options))];
}
