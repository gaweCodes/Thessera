using System.Text.Json;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Makes typed keys serialize as their bare value rather than as an object with a
/// <c>Value</c> property.
/// </summary>
/// <remarks>
/// The runtime applies this to what it serializes itself. Apply it to your own options whenever you
/// serialize domain types — an API response, a cache entry — so that the same identity does not
/// appear as <c>"id": "…"</c> in one place and <c>"id": { "value": "…" }</c> in another.
/// </remarks>
public static class EntityKeyJsonOptions
{
    /// <summary>
    /// Adds the typed-key converter to existing options.
    /// </summary>
    /// <param name="options">The options to extend.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Call this before the options are first used to serialize: <c>System.Text.Json</c> freezes
    /// them at that point and rejects later changes.
    /// </remarks>
    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new EntityKeyJsonConverterFactory());
    }

    /// <summary>
    /// Creates fresh options with the typed-key converter already added.
    /// </summary>
    /// <returns>Default general-purpose options that understand typed keys.</returns>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        Apply(options);
        return options;
    }
}
