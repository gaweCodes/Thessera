using System.Text.Json;

namespace GaWeCodes.Thessera.Core.Persistence;

public static class EntityKeyJsonOptions
{
    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new EntityKeyJsonConverterFactory());
    }

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        Apply(options);
        return options;
    }
}
