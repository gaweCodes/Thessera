using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags a child entity state that names a different type as its own <c>TSelf</c> - the same
/// mistake <see cref="AggregateStateSelfBindingAnalyzer"/> flags on <c>AggregateState&lt;TSelf, TKey&gt;</c>,
/// caught here for <c>EntityState&lt;TSelf, TKey&gt;</c> instead.
/// </summary>
/// <remarks>
/// Nothing at run time catches this today: the runtime's <c>AggregateStateSelfBindingCheck</c> walks
/// only the aggregate's own scanned assemblies for <c>AggregateState&lt;,&gt;</c> mismatches and does
/// not inspect <c>EntityState&lt;,&gt;</c> at all. The failure mode is identical to the aggregate
/// case - a copy-pasted declaration that names the wrong sibling compiles cleanly and only fails as
/// an <see cref="InvalidCastException"/> the first time the child applies an event.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ChildEntityStateSelfBindingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Child entity state does not name itself as TSelf",
        messageFormat: "Child entity state '{0}' declares '{1}' as its own type; a child entity " +
            "state must name itself as the first type argument of EntityState, because that is what " +
            "lets it return a copy of itself when an event is applied",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type deriving from EntityState<TSelf, TKey> must name itself as TSelf. The " +
            "generic constraint accepts a sibling state that happens to close the same TKey, so this " +
            "is checked separately.");

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
            var entityState = compilationContext.Compilation.GetTypeByMetadataName(
                ThesseraSymbols.EntityStateMetadataName);

            if (entityState is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, entityState),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol entityState)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false })
        {
            return;
        }

        var closedBase = type.FindClosedGenericBase(entityState);

        if (closedBase is null)
        {
            return;
        }

        var declaredSelf = closedBase.TypeArguments[0];

        if (SymbolEqualityComparer.Default.Equals(declaredSelf, type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            type.Locations.IsEmpty ? Location.None : type.Locations[0],
            type.Name,
            declaredSelf.Name));
    }
}
