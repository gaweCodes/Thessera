using GaWeCodes.Thessera.Domain.Aggregates;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Reflects the key type an aggregate declares through <c>IAggregateRoot&lt;TKey&gt;</c>.
/// </summary>
/// <remarks>
/// A store adapter uses this to register a closed-generic <c>IRepository&lt;TAggregate, TKey&gt;</c>
/// for an aggregate named in a <c>forAggregates</c> list, without the caller having to spell out its
/// key type a second time.
/// </remarks>
public static class AggregateKeyType
{
    /// <summary>
    /// Finds the key type <paramref name="aggregateType"/> declares.
    /// </summary>
    /// <param name="aggregateType">An aggregate root type.</param>
    /// <returns>The <c>TKey</c> from the <c>IAggregateRoot&lt;TKey&gt;</c> it implements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aggregateType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="aggregateType"/> does not implement <c>IAggregateRoot&lt;TKey&gt;</c>.
    /// </exception>
    public static Type Of(Type aggregateType)
    {
        ArgumentNullException.ThrowIfNull(aggregateType);

        var contract = Array.Find(
            aggregateType.GetInterfaces(),
            static @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IAggregateRoot<>));

        return contract?.GenericTypeArguments[0]
            ?? throw new InvalidOperationException(
                $"'{aggregateType}' does not implement IAggregateRoot<TKey>, so its key type cannot be reflected. " +
                "Only a type derived from AggregateRoot or EventSourcedAggregateRoot can be named in a " +
                "'forAggregates' list.");
    }
}
