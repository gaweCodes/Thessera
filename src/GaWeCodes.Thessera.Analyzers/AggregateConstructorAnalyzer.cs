using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags an aggregate whose parameterless constructor is missing or public - the compile-time twin
/// of two things <c>GaWeCodes.Thessera.Testing.AggregateConventions.Verify</c> checks in a test, and
/// that a store's repository otherwise discovers only when it first tries to reconstitute the type.
/// </summary>
/// <remarks>
/// A repository reconstitutes an aggregate through its parameterless constructor before replaying or
/// restoring its state; without one, no repository can be built for it. A <em>public</em> one is the
/// opposite problem: it lets the aggregate come into existence without going through the factory
/// method that checks its rules.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AggregateConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Aggregate constructor does not satisfy the reconstitution rule",
        messageFormat: "{0}",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An aggregate needs exactly one parameterless constructor, and it must not be " +
            "public: a repository reconstitutes an empty hull through it, while callers go through a " +
            "named factory method instead.");

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

            if (aggregateRoot is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, aggregateRoot),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol aggregateRoot)
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

        var parameterless = type.InstanceConstructors.FirstOrDefault(static ctor => ctor.Parameters.Length == 0);

        if (parameterless is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                type.Locations.IsEmpty ? Location.None : type.Locations[0],
                $"Aggregate '{type.Name}' has no parameterless constructor, so no repository can " +
                    "reconstitute it. Add a private one for the repository to use."));
            return;
        }

        if (parameterless.DeclaredAccessibility == Accessibility.Public)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                parameterless.Locations.IsEmpty ? type.Locations.FirstOrDefault() ?? Location.None : parameterless.Locations[0],
                $"Aggregate '{type.Name}' exposes a public parameterless constructor, so it can be " +
                    "created without going through its factory. Make it private."));
        }
    }
}
