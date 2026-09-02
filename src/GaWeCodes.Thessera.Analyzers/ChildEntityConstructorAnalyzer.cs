using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags a child entity that exposes a public constructor - the compile-time twin of the check
/// <c>GaWeCodes.Thessera.Testing.AggregateConventions.Verify</c> performs in a test. Nothing at run
/// time catches this today.
/// </summary>
/// <remarks>
/// A child entity is reached through its aggregate root, never through a repository of its own. A
/// child built without its root would have no channel to raise events into and no state to read, and
/// the failure would appear only when someone tried to use it - keep the constructor
/// <see langword="internal"/>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ChildEntityConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0004";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Child entity exposes a public constructor",
        messageFormat: "Child entity '{0}' exposes a public constructor, so a hull can be built " +
            "without its aggregate root and would have no channel to raise events through; keep the " +
            "constructor internal",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every type deriving from Entity<TKey, TState> must keep its constructors " +
            "internal; it is reached through its aggregate root, never through a repository of its " +
            "own.");

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
            var entity = compilationContext.Compilation.GetTypeByMetadataName(ThesseraSymbols.EntityMetadataName);

            if (entity is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, entity),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol entity)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false })
        {
            return;
        }

        if (!type.DerivesFromOpenGeneric(entity))
        {
            return;
        }

        var publicConstructor = type.InstanceConstructors
            .FirstOrDefault(static ctor => ctor.DeclaredAccessibility == Accessibility.Public);

        if (publicConstructor is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            publicConstructor.Locations.IsEmpty ? type.Locations.FirstOrDefault() ?? Location.None : publicConstructor.Locations[0],
            type.Name));
    }
}
