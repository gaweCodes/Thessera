using System.Linq.Expressions;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Core.Persistence;

public static class EntityKeyActivator
{
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
