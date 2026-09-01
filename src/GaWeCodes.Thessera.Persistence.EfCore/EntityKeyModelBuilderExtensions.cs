using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GaWeCodes.Thessera.Persistence.EfCore;

/// <summary>
/// Teaches an EF Core model to store typed keys as their bare value.
/// </summary>
public static class EntityKeyModelBuilderExtensions
{
    private static readonly ConcurrentDictionary<Type, ValueConverter> Converters = new();

    /// <summary>
    /// Gives every typed-key property in the model a value converter, so a column holds a
    /// <c>uuid</c> or a <c>bigint</c> rather than a serialized object.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <returns>The same <paramref name="modelBuilder"/>, so the call can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modelBuilder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Call this <strong>last</strong> in <c>OnModelCreating</c>, after the entities are configured:
    /// it walks the model as it stands, and a property configured afterwards is not seen. A property
    /// that already has a converter of your own is left alone.
    /// <para>
    /// The conversion is style-neutral and useful on a read context of an event-sourced service too,
    /// even though the rest of this package is about state storage.
    /// </para>
    /// </remarks>
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
