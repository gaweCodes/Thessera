using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Renders an aggregate identity as the text that addresses its event stream, in a format that is
/// pinned rather than left to whatever <c>ToString()</c> happens to produce.
/// </summary>
/// <remarks>
/// This text is not an internal detail: it is the stream key in the event store, it appears in
/// persisted rows, and it travels on every domain-event envelope. Once written it is permanent, so
/// the rendering may not depend on the runtime, the current culture or a framework default.
/// </remarks>
public static class EntityKeyFormatter
{
    /// <summary>
    /// The character between the aggregate name and the key value in a stream key.
    /// </summary>
    /// <remarks>
    /// Part of the wire format, which is why a string key containing it is refused: such a value
    /// would let two different aggregates address the same stream.
    /// </remarks>
    public const char StreamKeySeparator = '/';

    private static readonly ConcurrentDictionary<Type, Func<object, string>> KeyValueFormatters = new();
    private static readonly ConcurrentDictionary<Type, string> AggregateNames = new();

    /// <summary>
    /// Reads the persisted name of an aggregate type.
    /// </summary>
    /// <param name="aggregateType">The aggregate type.</param>
    /// <returns>The name from its <c>[AggregateName]</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The type has no <c>[AggregateName]</c>. The name prefixes every stream and travels on every
    /// envelope, so it is a persistence contract and is not defaulted to the CLR type name.
    /// </exception>
    public static string GetAggregateName(Type aggregateType) =>
        AggregateNames.GetOrAdd(aggregateType, ReadAggregateName);

    /// <summary>
    /// Renders a typed key as the text that goes into a stream key.
    /// </summary>
    /// <param name="key">The typed key.</param>
    /// <returns>
    /// The rendering pinned for the key value type: a <see cref="Guid"/> in format <c>D</c>,
    /// invariant; an <see cref="int"/> or <see cref="long"/> as an invariant decimal; a
    /// <see cref="string"/> verbatim.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The key is empty, so the aggregate was never given an identity; or it is a string containing
    /// the separator; or it is a negative number, which is almost always an uninitialised value; or
    /// its value type is none of the four with a declared format — a <see cref="decimal"/> keeps
    /// trailing zeros, an enum writes a member name and a date follows a calendar convention, each
    /// of which turns existing streams unreachable the day it changes.
    /// </exception>
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

    /// <summary>
    /// Builds the stream key from an aggregate name and an already rendered key value.
    /// </summary>
    /// <param name="aggregateName">The aggregate name.</param>
    /// <param name="keyValue">The rendered key value.</param>
    /// <returns>The stream key, as <c>&lt;aggregate-name&gt;/&lt;key-value&gt;</c>.</returns>
    public static string GetStreamKey(string aggregateName, string keyValue) =>
        string.Create(CultureInfo.InvariantCulture, $"{aggregateName}{StreamKeySeparator}{keyValue}");

    /// <summary>
    /// Builds the prefix every stream of one aggregate type shares.
    /// </summary>
    /// <param name="aggregateName">The aggregate name.</param>
    /// <returns>
    /// The prefix including the separator, so it can be used to find every stream of that type
    /// without also matching an aggregate whose name merely starts the same way.
    /// </returns>
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
