using System.Linq.Expressions;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Rebuilds a typed key from the bare value a store or a serializer read back.
/// </summary>
/// <remarks>
/// Written against the key's public constructor rather than reflection over its fields, so a key
/// stays an ordinary type with no attribute and no interface beyond <see cref="IEntityKey{TValue}"/>.
/// The factory is compiled once per key type and cached.
/// </remarks>
public static class EntityKeyActivator
{
    /// <summary>
    /// Wraps a value back into its typed key.
    /// </summary>
    /// <typeparam name="TKey">The key type to build.</typeparam>
    /// <typeparam name="TValue">The value it wraps.</typeparam>
    /// <param name="value">The value read back from the store or the wire.</param>
    /// <returns>The typed key carrying <paramref name="value"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TKey"/> has no public constructor taking a single
    /// <typeparamref name="TValue"/>. A key with only a parameterless constructor or an internal one
    /// cannot be rebuilt.
    /// </exception>
    public static TKey Create<TKey, TValue>(TValue value)
        where TKey : IEntityKey<TValue>
        where TValue : notnull
        => Cache<TKey, TValue>.CompiledFactory.Value(value);

    private static class Cache<TKey, TValue>
        where TKey : IEntityKey<TValue>
        where TValue : notnull
    {
        public static readonly Lazy<Func<TValue, TKey>> CompiledFactory = new(BuildFactory);

        private static Func<TValue, TKey> BuildFactory()
        {
            var constructor = typeof(TKey).GetConstructor([typeof(TValue)])
                ?? throw new InvalidOperationException(
                    $"The key type '{typeof(TKey)}' must expose a public constructor taking a single '{typeof(TValue)}' argument.");

            var parameter = Expression.Parameter(typeof(TValue), "value");
            return Expression.Lambda<Func<TValue, TKey>>(Expression.New(constructor, parameter), parameter).Compile();
        }
    }
}
