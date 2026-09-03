using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags a command handler whose constructor injects a store's own write session directly - an EF
/// Core <c>DbContext</c> or a Marten <c>IDocumentSession</c> - instead of only
/// <c>IRepository&lt;TAggregate, TKey&gt;</c>.
/// </summary>
/// <remarks>
/// <c>IRepository&lt;TAggregate, TKey&gt;</c> deliberately has no <c>Save</c>: the unit of work
/// commits once per command, and only inside that one commit does an aggregate's state (or appended
/// events) and the domain-event envelopes that carry its raised events to the outbox get written
/// together, in one transaction. A handler that also injects the underlying <c>DbContext</c> or
/// <c>IDocumentSession</c> can call <c>SaveChangesAsync</c> itself, which commits the aggregate's
/// change in its own, separate transaction - one with no outbox row. If the process then stops
/// before the pipeline's own, later commit runs, that change is durable but its domain event will
/// never be published; if the pipeline's commit does still run afterwards, whether the event still
/// gets published depends on store-specific, unreliable timing (a second EF Core <c>SaveChanges</c>
/// that may find nothing left to save, or a second Marten append at a version the manual save already
/// moved past). Either way this is a silent hole in the guarantee this family is built on, so this
/// rule flags the dependency itself, unconditionally - not the particular call the handler happens to
/// make with it, and regardless of whether the handler also injects <c>IRepository&lt;,&gt;</c>.
/// <para>
/// A query handler is not in scope: <c>IQueryHandler&lt;,&gt;</c> reads only, has no unit of work to
/// bypass, and injecting a <c>DbContext</c> directly for a read (typically with
/// <c>AsNoTracking()</c>) is the family's own documented pattern.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandHandlerRawSessionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0008";

    private const string DbContextMetadataName = "Microsoft.EntityFrameworkCore.DbContext";
    private const string MartenDocumentSessionMetadataName = "Marten.IDocumentSession";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Command handler injects a store's raw session directly",
        messageFormat: "Command handler '{0}' injects '{1}' directly - a {2} session - bypassing " +
            "IUnitOfWork; inject IRepository<TAggregate, TKey> instead so the aggregate's state and " +
            "its domain events commit together",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A unit of work commits an aggregate's state (or event stream) and its outbox " +
            "envelopes in one transaction. A command handler that also injects the underlying " +
            "DbContext or IDocumentSession can call SaveChangesAsync itself, splitting that one " +
            "transaction in two and risking a change that is durable while its domain event is never " +
            "published.");

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
            var commandHandler = compilationContext.Compilation.GetTypeByMetadataName(ThesseraSymbols.CommandHandlerMetadataName);
            var commandHandlerWithResult = compilationContext.Compilation.GetTypeByMetadataName(ThesseraSymbols.CommandHandlerWithResultMetadataName);

            if (commandHandler is null && commandHandlerWithResult is null)
            {
                return;
            }

            var dbContext = compilationContext.Compilation.GetTypeByMetadataName(DbContextMetadataName);
            var documentSession = compilationContext.Compilation.GetTypeByMetadataName(MartenDocumentSessionMetadataName);

            if (dbContext is null && documentSession is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, commandHandler, commandHandlerWithResult, dbContext, documentSession),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(
        SymbolAnalysisContext context,
        INamedTypeSymbol? commandHandler,
        INamedTypeSymbol? commandHandlerWithResult,
        INamedTypeSymbol? dbContext,
        INamedTypeSymbol? documentSession)
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
            foreach (var parameter in constructor.Parameters)
            {
                var storeKind = RawStoreKind(parameter.Type, dbContext, documentSession);

                if (storeKind is null)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    parameter.Locations.IsEmpty ? type.Locations.FirstOrDefault() ?? Location.None : parameter.Locations[0],
                    type.Name,
                    parameter.Type.Name,
                    storeKind));
            }
        }
    }

    private static string? RawStoreKind(ITypeSymbol parameterType, INamedTypeSymbol? dbContext, INamedTypeSymbol? documentSession) =>
        dbContext is not null && parameterType.DerivesFromOrIs(dbContext)
            ? "EF Core"
            : documentSession is not null && parameterType.ImplementsOrIs(documentSession) ? "Marten" : null;
}
