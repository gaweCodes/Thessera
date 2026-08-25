using System.Text.Json;
using System.Text.Json.Serialization;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Core.Persistence;

internal sealed class EntityKeyJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => FindKeyInterface(typeToConvert) is not null;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var keyInterface = FindKeyInterface(typeToConvert)
            ?? throw new InvalidOperationException(
                $"The type '{typeToConvert}' does not implement IEntityKey<TValue>.");

        var converterType = typeof(EntityKeyJsonConverter<,>)
            .MakeGenericType(typeToConvert, keyInterface.GenericTypeArguments[0]);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private static Type? FindKeyInterface(Type typeToConvert) => Array.Find(
        typeToConvert.GetInterfaces(),
        static @interface => @interface.IsGenericType
            && @interface.GetGenericTypeDefinition() == typeof(IEntityKey<>));
}
