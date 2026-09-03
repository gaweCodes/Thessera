using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags a command handler whose constructor injects <c>IRepository&lt;TAggregate, TKey&gt;</c> for
/// more than one aggregate - the compile-time twin of the runtime's
/// <c>CommandStoreRoutingCheck</c>, which throws at startup for exactly the same shape once a host
/// selects more than one persistence store.
/// </summary>
/// <remarks>
/// A command is one unit of work against one store: the unit of work commits once per command, and
/// when a host mixes an event-sourced and a state-stored aggregate, that one commit can only ever
/// belong to one of the two stores. A handler that reaches into repositories for two different
/// aggregates therefore cannot be routed to a single store even before a second store is ever
/// configured - so this rule reports it unconditionally, independent of how many stores the host
/// under edit actually selects.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandHandlerSingleStoreAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0007";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Command handler reaches across more than one aggregate",
        messageFormat: "Command handler '{0}' injects repositories for more than one aggregate ({1}), " +
            "so it cannot be routed to a single store; split it into one handler per aggregate",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A command commits through exactly one unit of work, so a command handler must " +
            "inject IRepository<TAggregate, TKey> for exactly one aggregate type. A handler spanning " +
            "several aggregates cannot be routed to a single store once a host mixes an event-sourced " +
            "and a state-stored aggregate.");

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
            var repository = compilationContext.Compilation.GetTypeByMetadataName(ThesseraSymbols.RepositoryMetadataName);

            if (repository is null)
            {
                return;
            }

            var commandHandler = compilationContext.Compilation.GetTypeByMetadataName(ThesseraSymbols.CommandHandlerMetadataName);
            var commandHandlerWithResult = compilationContext.Compilation.GetTypeByMetadataName(ThesseraSymbols.CommandHandlerWithResultMetadataName);

            if (commandHandler is null && commandHandlerWithResult is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, repository, commandHandler, commandHandlerWithResult),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(
        SymbolAnalysisContext context,
        INamedTypeSymbol repository,
        INamedTypeSymbol? commandHandler,
        INamedTypeSymbol? commandHandlerWithResult)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false })
        {
            return;
        }

        var isCommandHandler =
            (commandHandler is not null && type.ImplementsOpenGeneric(commandHandler)) ||
            (commandHandlerWithResult is not null && type.ImplementsOpenGeneric(commandHandlerWithResult));

        if (!isCommandHandler)
        {
            return;
        }

        foreach (var constructor in type.InstanceConstructors)
        {
            var aggregates = constructor.Parameters
                .Select(parameter => ClaimedAggregate(parameter.Type, repository))
                .Where(static aggregate => aggregate is not null)
                .Select(static aggregate => aggregate!)
                .Distinct<ITypeSymbol>(SymbolEqualityComparer.Default)
                .ToImmutableArray();

            if (aggregates.Length <= 1)
            {
                continue;
            }

            var names = string.Join(", ", aggregates.Select(static aggregate => aggregate.Name));

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                constructor.Locations.IsEmpty ? type.Locations.FirstOrDefault() ?? Location.None : constructor.Locations[0],
                type.Name,
                names));
        }
    }

    private static ITypeSymbol? ClaimedAggregate(ITypeSymbol parameterType, INamedTypeSymbol repository) =>
        parameterType is INamedTypeSymbol { TypeArguments.Length: 2 } named &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, repository)
            ? named.TypeArguments[0]
            : null;
}
