using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags an aggregate that has no <c>[AggregateName]</c> - the compile-time twin of the check
/// <c>GaWeCodes.Thessera.Testing.AggregateConventions.Verify</c> performs in a test and that the
/// event-catalogue build performs unconditionally at startup.
/// </summary>
/// <remarks>
/// The persisted name prefixes every stream key and travels on every domain-event envelope the
/// aggregate produces; a type missing it is rejected rather than silently named after its CLR type,
/// because renaming the class later would otherwise orphan every stream already written under the
/// old name.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AggregateNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Aggregate is missing [AggregateName]",
        messageFormat: "Aggregate '{0}' has no [AggregateName]; the name prefixes every stream key and " +
            "travels on every domain-event envelope it produces, so renaming the class later would " +
            "orphan every stream already written under the old one",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every type deriving from AggregateRoot<TKey, TState> or " +
            "EventSourcedAggregateRoot<TKey, TState> must declare its persisted name with " +
            "[AggregateName].");

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
            var aggregateRoot = compilationContext.Compilation.GetTypeByMetadataName(
                ThesseraSymbols.AggregateRootMetadataName);
            var aggregateNameAttribute = compilationContext.Compilation.GetTypeByMetadataName(
                ThesseraSymbols.AggregateNameAttributeMetadataName);

            if (aggregateRoot is null || aggregateNameAttribute is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, aggregateRoot, aggregateNameAttribute),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(
        SymbolAnalysisContext context,
        INamedTypeSymbol aggregateRoot,
        INamedTypeSymbol aggregateNameAttribute)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false })
        {
            return;
        }

        if (!type.DerivesFromOpenGeneric(aggregateRoot))
        {
            return;
        }

        if (type.HasAttributeDeclaredDirectly(aggregateNameAttribute))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations.IsEmpty ? Location.None : type.Locations[0], type.Name));
    }
}
