using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace GaWeCodes.Thessera.Analyzers;

/// <summary>
/// Flags a call to <c>RaiseEvent</c> whose argument is a retired domain event - the compile-time
/// twin of the check <c>AggregateRoot.RaiseEvent</c> performs on every call.
/// </summary>
/// <remarks>
/// A type implementing <c>IRetiredDomainEvent&lt;TSuccessor&gt;</c> is kept only so that stored
/// occurrences can be read, and every read upcasts it to its successor; raising it would store an
/// event that is never read as it was written.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RetiredDomainEventRaisedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id this analyzer reports.</summary>
    public const string DiagnosticId = "THSS0009";

    private const string RaiseEventMethodName = "RaiseEvent";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Retired domain event is raised",
        messageFormat: "Domain event '{0}' is retired and is only ever read as its successor; raise the " +
            "successor instead",
        category: "Thessera.Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type implementing IRetiredDomainEvent<TSuccessor> is kept for reading stored " +
            "events only and must not be passed to RaiseEvent.");

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
            var compilation = compilationContext.Compilation;
            var aggregateRoot = compilation.ResolveType(
                ThesseraSymbols.AggregateRootMetadataName, ThesseraSymbols.DomainAssemblyName);
            var entity = compilation.ResolveType(
                ThesseraSymbols.EntityMetadataName, ThesseraSymbols.DomainAssemblyName);
            var retiredDomainEvent = compilation.ResolveType(
                ThesseraSymbols.RetiredDomainEventMetadataName, ThesseraSymbols.DomainAssemblyName);

            if (aggregateRoot is null || entity is null || retiredDomainEvent is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => Analyze(operationContext, aggregateRoot, entity, retiredDomainEvent),
                OperationKind.Invocation);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        INamedTypeSymbol aggregateRoot,
        INamedTypeSymbol entity,
        INamedTypeSymbol retiredDomainEvent)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name != RaiseEventMethodName || invocation.Arguments.Length != 1)
        {
            return;
        }

        var declaringType = method.ContainingType.OriginalDefinition;

        if (!SymbolEqualityComparer.Default.Equals(declaringType, aggregateRoot)
            && !SymbolEqualityComparer.Default.Equals(declaringType, entity))
        {
            return;
        }

        var argument = invocation.Arguments[0].Value;
        var raisedType = argument is IConversionOperation conversion ? conversion.Operand.Type : argument.Type;

        if (raisedType is null || !IsRetired(raisedType, retiredDomainEvent))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Syntax.GetLocation(), raisedType.Name));
    }

    private static bool IsRetired(ITypeSymbol type, INamedTypeSymbol retiredDomainEvent) =>
        SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, retiredDomainEvent)
        || type.ImplementsOpenGeneric(retiredDomainEvent);
}
