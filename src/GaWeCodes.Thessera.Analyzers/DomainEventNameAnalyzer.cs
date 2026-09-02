using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags a domain event that has no <c>[EventName]</c> - the compile-time twin of the check
/// <c>GaWeCodes.Thessera.Testing.AggregateConventions.Verify</c> performs in a test and that the
/// runtime's domain-event catalogue build performs unconditionally at startup.
/// </summary>
/// <remarks>
/// The persisted name is what a stored event and its envelope carry, and what an incoming event is
/// resolved back to a type by; the CLR type name is not a persistence contract.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainEventNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Domain event is missing [EventName]",
        messageFormat: "Domain event '{0}' has no [EventName]; the class name is not a persistence " +
            "contract, and without this attribute the event can be written but never resolved back " +
            "from what was stored",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every type implementing IDomainEvent must declare its persisted name with " +
            "[EventName].");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var domainEvent = compilationContext.Compilation.GetTypeByMetadataName(
                ThesseraSymbols.DomainEventInterfaceMetadataName);
            var eventNameAttribute = compilationContext.Compilation.GetTypeByMetadataName(
                ThesseraSymbols.EventNameAttributeMetadataName);

            if (domainEvent is null || eventNameAttribute is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, domainEvent, eventNameAttribute),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(
        SymbolAnalysisContext context,
        INamedTypeSymbol domainEvent,
        INamedTypeSymbol eventNameAttribute)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false })
        {
            return;
        }

        if (!type.Implements(domainEvent))
        {
            return;
        }

        if (type.HasAttributeDeclaredDirectly(eventNameAttribute))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations.IsEmpty ? Location.None : type.Locations[0], type.Name));
    }
}
