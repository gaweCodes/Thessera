using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Testing;

internal static class PersistedSchemaRenderer
{
    private const string Indent = "  ";

    private static readonly Dictionary<Type, string> LeafNames = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(char)] = "char",
        [typeof(string)] = "string",
        [typeof(Guid)] = "guid",
        [typeof(DateTime)] = "datetime",
        [typeof(DateTimeOffset)] = "datetimeoffset",
        [typeof(DateOnly)] = "dateonly",
        [typeof(TimeOnly)] = "timeonly",
        [typeof(TimeSpan)] = "timespan",
        [typeof(Uri)] = "uri",
    };

    public static string Render(IEnumerable<Assembly> assemblies)
    {
        var persistedTypes = new List<Type>();

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            persistedTypes.AddRange(PersistedTypesIn(assembly));
        }

        return Render(persistedTypes);
    }

    public static string Render(IEnumerable<Type> persistedTypes)
    {
        var options = EntityKeyJsonOptions.Create();
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();

        var rendered = new HashSet<Type>();
        var blocks = new List<string>();

        foreach (var persistedType in persistedTypes)
        {
            if (rendered.Add(persistedType))
            {
                blocks.Add(RenderBlock(persistedType, options));
            }
        }

        blocks.Sort(StringComparer.Ordinal);

        return string.Join('\n', blocks);
    }

    private static IEnumerable<Type> PersistedTypesIn(Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            throw new InvalidOperationException(
                $"The types of assembly '{assembly.FullName}' could not be loaded. " +
                "The most common cause is a missing package reference.",
                exception);
        }

        return types.Where(static type =>
            type is { IsClass: true, IsAbstract: false }
            && !type.IsGenericTypeDefinition
            && (typeof(IDomainEvent).IsAssignableFrom(type)
                || typeof(IIntegrationEvent).IsAssignableFrom(type)
                || AggregateKeyType(type) is not null));
    }

    private static Type? AggregateKeyType(Type type) =>
        Array.Find(
            type.GetInterfaces(),
            static @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IAggregateRoot<>))
            ?.GenericTypeArguments[0];

    private static string RenderBlock(Type persistedType, JsonSerializerOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(Header(persistedType)).Append('\n');

        if (AggregateKeyType(persistedType) is null)
        {
            RenderMembers(persistedType, options, builder, depth: 1, [persistedType]);
        }

        return builder.ToString();
    }

    private static string Header(Type persistedType)
    {
        var aggregateKeyType = AggregateKeyType(persistedType);
        if (aggregateKeyType is not null)
        {
            var aggregateName = persistedType.GetCustomAttribute<AggregateNameAttribute>(inherit: false)?.Name
                ?? throw new InvalidOperationException(
                    $"The aggregate '{persistedType}' has no [AggregateName] and therefore has no stream key to " +
                    "snapshot.");

            return $"aggregate-stream {aggregateName}/{LeafName(aggregateKeyType)}";
        }

        if (typeof(IDomainEvent).IsAssignableFrom(persistedType))
        {
            var name = persistedType.GetCustomAttribute<EventNameAttribute>(inherit: false)?.Name
                ?? throw new InvalidOperationException(
                    $"The domain event '{persistedType}' has no [EventName] and therefore has no persisted name " +
                    "to snapshot.");

            return $"domain-event {name}";
        }

        var topic = persistedType.GetCustomAttribute<IntegrationEventTopicAttribute>(inherit: false)?.Topic
            ?? throw new InvalidOperationException(
                $"The integration event '{persistedType}' has no [IntegrationEventTopic] and therefore has no " +
                "published routing key to snapshot.");

        return $"integration-event {topic}";
    }

    private static void RenderMembers(
        Type objectType,
        JsonSerializerOptions options,
        StringBuilder builder,
        int depth,
        HashSet<Type> path)
    {
        foreach (var property in options.GetTypeInfo(objectType).Properties
            .OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
            RenderMember(property.Name, property.PropertyType, options, builder, depth, path);
        }
    }

    private static void RenderMember(
        string name,
        Type memberType,
        JsonSerializerOptions options,
        StringBuilder builder,
        int depth,
        HashSet<Type> path)
    {
        var (label, nested) = Describe(memberType, options);

        for (var level = 0; level < depth; level++)
        {
            builder.Append(Indent);
        }

        builder.Append(name).Append(" : ").Append(label);

        if (nested is null)
        {
            builder.Append('\n');
            return;
        }

        if (!path.Add(nested))
        {
            builder.Append(" (recursive)").Append('\n');
            return;
        }

        builder.Append('\n');
        RenderMembers(nested, options, builder, depth + 1, path);
        path.Remove(nested);
    }

    private static (string Label, Type? Nested) Describe(Type memberType, JsonSerializerOptions options)
    {
        var underlying = Nullable.GetUnderlyingType(memberType);
        if (underlying is not null)
        {
            var (innerLabel, innerNested) = Describe(underlying, options);
            return (innerLabel + "?", innerNested);
        }

        var typeInfo = options.GetTypeInfo(memberType);

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object:
                return ("object", memberType);

            case JsonTypeInfoKind.Enumerable when typeInfo.ElementType is not null:
                var (elementLabel, elementNested) = Describe(typeInfo.ElementType, options);
                return (elementLabel + "[]", elementNested);

            case JsonTypeInfoKind.Dictionary when typeInfo.KeyType is not null && typeInfo.ElementType is not null:
                var (keyLabel, _) = Describe(typeInfo.KeyType, options);
                var (valueLabel, valueNested) = Describe(typeInfo.ElementType, options);
                return ($"map<{keyLabel},{valueLabel}>", valueNested);

            default:
                return (LeafName(memberType), null);
        }
    }

    private static string LeafName(Type leafType)
    {
        if (LeafNames.TryGetValue(leafType, out var name))
        {
            return name;
        }

        var keyInterface = Array.Find(
            leafType.GetInterfaces(),
            static @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IEntityKey<>));

        return keyInterface is not null
            ? LeafName(keyInterface.GenericTypeArguments[0])
            : leafType.IsEnum ? LeafName(Enum.GetUnderlyingType(leafType)) : leafType.Name;
    }
}
