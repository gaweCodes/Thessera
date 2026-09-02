using Microsoft.CodeAnalysis;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// The metadata names of the Thessera types every rule in this package reasons about, and the
/// symbol-matching helpers built on top of them.
/// </summary>
/// <remarks>
/// This package carries no reference to <c>GaWeCodes.Thessera.Domain</c> - an analyzer targets
/// <c>netstandard2.0</c> and runs inside the compiler process, not inside the consumer's build
/// output, so it cannot depend on the very assembly it is analyzing. Every rule instead resolves
/// these names against the compilation it is handed through <see cref="Compilation.GetTypeByMetadataName(string)"/>,
/// and does nothing when a name resolves to nothing - which is exactly the case where the compiled
/// project does not reference <c>GaWeCodes.Thessera.Domain</c> at all.
/// </remarks>
internal static class ThesseraSymbols
{
    /// <summary>The metadata name of <c>AggregateRoot&lt;TKey, TState&gt;</c>.</summary>
    internal const string AggregateRootMetadataName = "GaWeCodes.Thessera.Domain.Aggregates.AggregateRoot`2";

    /// <summary>The metadata name of <c>Entity&lt;TKey, TState&gt;</c>.</summary>
    internal const string EntityMetadataName = "GaWeCodes.Thessera.Domain.Entities.Entity`2";

    /// <summary>The metadata name of <c>IDomainEvent</c>.</summary>
    internal const string DomainEventInterfaceMetadataName = "GaWeCodes.Thessera.Domain.Events.IDomainEvent";

    /// <summary>The metadata name of <c>AggregateNameAttribute</c>.</summary>
    internal const string AggregateNameAttributeMetadataName = "GaWeCodes.Thessera.Domain.Naming.AggregateNameAttribute";

    /// <summary>The metadata name of <c>EventNameAttribute</c>.</summary>
    internal const string EventNameAttributeMetadataName = "GaWeCodes.Thessera.Domain.Naming.EventNameAttribute";

    /// <summary>The metadata name of <c>AggregateState&lt;TSelf, TKey&gt;</c>.</summary>
    internal const string AggregateStateMetadataName = "GaWeCodes.Thessera.Domain.Aggregates.AggregateState`2";

    /// <summary>The metadata name of <c>EntityState&lt;TSelf, TKey&gt;</c>.</summary>
    internal const string EntityStateMetadataName = "GaWeCodes.Thessera.Domain.Entities.EntityState`2";

    /// <summary>
    /// Answers whether <paramref name="type"/> derives - directly or indirectly - from the open
    /// generic type <paramref name="openGenericBase"/> resolves to.
    /// </summary>
    internal static bool DerivesFromOpenGeneric(this ITypeSymbol? type, INamedTypeSymbol openGenericBase)
    {
        for (var current = type?.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, openGenericBase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Walks the base-type chain of <paramref name="type"/> for the first type closing the open
    /// generic type <paramref name="openGenericBase"/> resolves to, and returns it.
    /// </summary>
    /// <remarks>
    /// Mirrors the walk the runtime's <c>AggregateStateSelfBindingCheck</c> performs by reflection:
    /// the nearest closing base in the chain is the one whose first type argument names the type
    /// that is supposed to be <em>self</em>.
    /// </remarks>
    internal static INamedTypeSymbol? FindClosedGenericBase(this ITypeSymbol? type, INamedTypeSymbol openGenericBase)
    {
        for (var current = type?.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, openGenericBase))
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Answers whether <paramref name="type"/> implements <paramref name="interfaceType"/>.</summary>
    internal static bool Implements(this ITypeSymbol type, INamedTypeSymbol interfaceType) =>
        type.AllInterfaces.Contains(interfaceType, SymbolEqualityComparer.Default);

    /// <summary>
    /// Answers whether <paramref name="symbol"/> itself - not something it derives from - carries
    /// <paramref name="attribute"/>. Mirrors <c>GetCustomAttribute(inherit: false)</c>, because these
    /// attributes are declared with <c>[AttributeUsage(Inherited = false)]</c>: a base type's name
    /// never silently becomes a derived type's name.
    /// </summary>
    internal static bool HasAttributeDeclaredDirectly(this ISymbol symbol, INamedTypeSymbol attribute)
    {
        foreach (var candidate in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attribute))
            {
                return true;
            }
        }

        return false;
    }
}
