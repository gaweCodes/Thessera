using Microsoft.CodeAnalysis;

namespace GaWeCodes.Thessera.Analyzers;

internal static class ThesseraSymbols
{
    internal const string AggregateRootMetadataName = "GaWeCodes.Thessera.Domain.Aggregates.AggregateRoot`2";

    internal const string EntityMetadataName = "GaWeCodes.Thessera.Domain.Entities.Entity`2";

    internal const string DomainEventInterfaceMetadataName = "GaWeCodes.Thessera.Domain.Events.IDomainEvent";

    internal const string AggregateNameAttributeMetadataName = "GaWeCodes.Thessera.Domain.Naming.AggregateNameAttribute";

    internal const string EventNameAttributeMetadataName = "GaWeCodes.Thessera.Domain.Naming.EventNameAttribute";

    internal const string AggregateStateMetadataName = "GaWeCodes.Thessera.Domain.Aggregates.AggregateState`2";

    internal const string EntityStateMetadataName = "GaWeCodes.Thessera.Domain.Entities.EntityState`2";

    internal const string CommandHandlerMetadataName = "GaWeCodes.Thessera.Application.Cqrs.ICommandHandler`1";

    internal const string CommandHandlerWithResultMetadataName = "GaWeCodes.Thessera.Application.Cqrs.ICommandHandler`2";

    internal const string RepositoryMetadataName = "GaWeCodes.Thessera.Application.Persistence.IRepository`2";

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

    internal static bool Implements(this ITypeSymbol type, INamedTypeSymbol interfaceType) =>
        type.AllInterfaces.Contains(interfaceType, SymbolEqualityComparer.Default);

    internal static bool ImplementsOrIs(this ITypeSymbol type, INamedTypeSymbol interfaceType) =>
        SymbolEqualityComparer.Default.Equals(type, interfaceType) || type.Implements(interfaceType);

    internal static bool DerivesFromOrIs(this ITypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ImplementsOpenGeneric(this ITypeSymbol type, INamedTypeSymbol openGenericInterface) =>
        type.AllInterfaces.Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, openGenericInterface));

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
