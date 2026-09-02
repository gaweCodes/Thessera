using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags an aggregate state that names a different type as its own <c>TSelf</c> - the compile-time
/// twin of the runtime's <c>AggregateStateSelfBindingCheck</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>AggregateState&lt;TSelf, TKey&gt;</c> declares <c>where TSelf : AggregateState&lt;TSelf, TKey&gt;</c>,
/// but that constraint is checked against whatever type is named as <c>TSelf</c> - not against the
/// type doing the naming. Two states that already close the same <c>TKey</c> both satisfy each
/// other's constraint, so a copy-pasted declaration that names the wrong sibling compiles cleanly
/// and only fails as an <see cref="InvalidCastException"/> the first time the aggregate applies an
/// event.
/// </para>
/// <para>
/// This is a narrower claim than "the compiler already prevents this" - it prevents a
/// <em>mismatched <c>TKey</c></em>, never a same-<c>TKey</c> mix-up between sibling states.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AggregateStateSelfBindingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Aggregate state does not name itself as TSelf",
        messageFormat: "Aggregate state '{0}' declares '{1}' as its own type; an aggregate state " +
            "must name itself as the first type argument of AggregateState, because that is what " +
            "lets it return a copy of itself when an event is applied",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type deriving from AggregateState<TSelf, TKey> must name itself as TSelf. " +
            "The generic constraint accepts a sibling state that happens to close the same TKey, so " +
            "this is checked separately.");

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
            var aggregateState = compilationContext.Compilation.GetTypeByMetadataName(
                ThesseraSymbols.AggregateStateMetadataName);

            if (aggregateState is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, aggregateState),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol aggregateState)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false })
        {
            return;
        }

        var closedBase = type.FindClosedGenericBase(aggregateState);

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
