using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GaWeCodes.Thessera.Persistence.EfCore;

public static class EntityKeyModelBuilderExtensions
{
    private static readonly ConcurrentDictionary<Type, ValueConverter> Converters = new();

    [RequiresUnreferencedCode(TrimmingMessages.ModelReflection)]
    [RequiresDynamicCode(TrimmingMessages.ModelReflection)]
    public static ModelBuilder ApplyEntityKeyConversions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            ApplyConverters(entityType);
        }

        return modelBuilder;
    }

    private static void ApplyConverters(IMutableTypeBase type)
    {
        foreach (var property in type.GetProperties())
        {
            ApplyConverter(property);
        }

        foreach (var complexProperty in type.GetComplexProperties())
        {
            ApplyConverters(complexProperty.ComplexType);
        }
    }

    private static void ApplyConverter(IMutableProperty property)
    {
        if (property.GetValueConverter() is not null)
        {
            return;
        }

        var keyInterface = Array.Find(
            property.ClrType.GetInterfaces(),
            static @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IEntityKey<>));

        if (keyInterface is null)
        {
            return;
        }

        property.SetValueConverter(Converters.GetOrAdd(
            property.ClrType,
            static (keyType, valueType) => (ValueConverter)Activator.CreateInstance(
                typeof(EntityKeyValueConverter<,>).MakeGenericType(keyType, valueType))!,
            keyInterface.GetGenericArguments()[0]));
    }
}
