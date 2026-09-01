using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GaWeCodes.Thessera.Domain.Aggregates;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Creates the empty aggregate hull a repository fills from the store.
/// </summary>
/// <remarks>
/// Loading an aggregate does not go through its named factory — that factory exists to enforce the
/// rules for <em>creating</em> one, and a stored aggregate has already been created. The hull is
/// built through the private parameterless constructor and then restored or replayed into.
/// </remarks>
public static class AggregateFactory
{
    private static readonly ConcurrentDictionary<Type, ConstructorInfo> Constructors = new();

    /// <summary>
    /// Creates an empty instance of an aggregate, ready to be filled.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate to create.</typeparam>
    /// <returns>An instance with no state applied yet.</returns>
    /// <exception cref="InvalidOperationException">
    /// The type has no parameterless constructor, so no repository can reconstitute it. Add one and
    /// keep it private, so the named factory stays the only public way to create the aggregate.
    /// </exception>
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public static TAggregate CreateEmpty<TAggregate>()
        where TAggregate : class =>
        (TAggregate)ConstructorFor(typeof(TAggregate)).Invoke(null);

    internal static void EnsureAggregatesAreReconstitutable(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var aggregateType in AggregateTypesIn(assembly))
            {
                ConstructorFor(aggregateType);
            }
        }
    }

    private static IEnumerable<Type> AggregateTypesIn(Assembly assembly) =>
        assembly.GetTypes().Where(static type =>
            type is { IsClass: true, IsAbstract: false }
            && !type.IsGenericTypeDefinition
            && Array.Exists(
                type.GetInterfaces(),
                static contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IAggregateRoot<>)));

    private static ConstructorInfo ConstructorFor(Type aggregateType) =>
        Constructors.GetOrAdd(aggregateType, static type =>
            type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"'{type}' has no parameterless constructor. A repository reconstitutes an aggregate by creating " +
                "an empty hull through its parameterless constructor and filling it from the store; add one and " +
                "keep it private, so the aggregate's named factory stays the only public way to create one."));
}
