using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Persistence;

public static class EntityKeyFormatter
{
    public const char StreamKeySeparator = '/';

    private static readonly ConcurrentDictionary<Type, Func<object, string>> KeyValueFormatters = new();
    private static readonly ConcurrentDictionary<Type, string> AggregateNames = new();

    public static string GetAggregateName(Type aggregateType) =>
        AggregateNames.GetOrAdd(aggregateType, ReadAggregateName);

    [RequiresUnreferencedCode(TrimmingMessages.TypedKeyReflection)]
    [RequiresDynamicCode(TrimmingMessages.TypedKeyReflection)]
    public static string GetKeyValue(object key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key is IEntityKey { IsEmpty: true }
            ? throw new InvalidOperationException(
                $"The key '{key.GetType()}' is empty. An empty key means the aggregate was never given an identity, " +
                "and formatting it would produce a stream key that looks valid and is shared by every other " +
                "unidentified aggregate of that type. Assign the identity before saving.")
            : KeyValueFormatters.GetOrAdd(key.GetType(), CreateKeyValueFormatter)(key);
    }

    public static string GetStreamKey(string aggregateName, string keyValue) =>
        string.Create(CultureInfo.InvariantCulture, $"{aggregateName}{StreamKeySeparator}{keyValue}");

    public static string GetStreamKeyPrefix(string aggregateName) =>
        string.Create(CultureInfo.InvariantCulture, $"{aggregateName}{StreamKeySeparator}");

    private static string ReadAggregateName(Type aggregateType) =>
        aggregateType.GetCustomAttribute<AggregateNameAttribute>(inherit: false)?.Name
        ?? throw new InvalidOperationException(
            $"The aggregate '{aggregateType}' has no [AggregateName]. The name prefixes every event stream and " +
            "travels on every domain event envelope, so it is a persistence contract and must be chosen " +
            "deliberately instead of following the CLR type name.");

    private static Type ValueTypeOf(Type keyType)
    {
        var keyInterface = Array.Find(
            keyType.GetInterfaces(),
            static @interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEntityKey<>))
            ?? throw new InvalidOperationException(
                $"The key type '{keyType}' does not implement IEntityKey<TValue>.");

        return keyInterface.GenericTypeArguments[0];
    }

    private static Func<object, string> CreateKeyValueFormatter(Type keyType)
    {
        var valueType = ValueTypeOf(keyType);
        var readValue = CreateValueAccessor(valueType);

        return valueType == typeof(Guid)
            ? key => ((Guid)readValue(key)).ToString("D", CultureInfo.InvariantCulture)
            : valueType == typeof(string)
                ? key => FormatString(keyType, (string)readValue(key))
                : valueType == typeof(int)
                    ? key => FormatInteger(keyType, (int)readValue(key))
                    : valueType == typeof(long)
                        ? key => FormatInteger(keyType, (long)readValue(key))
                        : throw UnsupportedValueType(keyType, valueType);
    }

    private static string FormatString(Type keyType, string value) =>
        value.Contains(StreamKeySeparator, StringComparison.Ordinal)
            ? throw new InvalidOperationException(
                $"The key '{keyType}' has the value '{value}', which contains '{StreamKeySeparator}'. That character " +
                "separates the aggregate name from the key inside a stream key, so such a value lets two different " +
                "aggregates address the same stream. Choose a value without it.")
            : value;

    private static string FormatInteger(Type keyType, long value) =>
        value < 0
            ? throw new InvalidOperationException(
                $"The key '{keyType}' has the negative value {value.ToString(CultureInfo.InvariantCulture)}. A stream " +
                "key is an identity, and a negative number is almost always an uninitialised value or an error " +
                "marker that would silently create a stream of its own. Use non-negative keys.")
            : value.ToString("D", CultureInfo.InvariantCulture);

    private static InvalidOperationException UnsupportedValueType(Type keyType, Type valueType) =>
        new($"The key '{keyType}' carries the value type '{valueType}', which has no declared stream-key format. " +
            "A stream key is persisted forever, so its text may not be whatever the runtime happens to produce " +
            "today: a decimal keeps trailing zeros, an enum writes its member name and a date follows a calendar " +
            "convention, each of which turns an existing stream unreachable when it changes. Use Guid, string, int " +
            "or long.");

    private static Func<object, object> CreateValueAccessor(Type valueType)
    {
        var parameter = Expression.Parameter(typeof(object), "key");
        var body = Expression.Convert(
            Expression.Property(
                Expression.Convert(parameter, typeof(IEntityKey<>).MakeGenericType(valueType)),
                nameof(IEntityKey<>.Value)),
            typeof(object));

        return Expression.Lambda<Func<object, object>>(body, parameter).Compile();
    }
}
