using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class CommandHandlerRawSessionAnalyzerTests
{
    private const string Prelude = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using GaWeCodes.Thessera.Application.Cqrs;
        using GaWeCodes.Thessera.Application.Persistence;
        using GaWeCodes.Thessera.Application.Results;
        using GaWeCodes.Thessera.Domain.Aggregates;
        using GaWeCodes.Thessera.Domain.Entities;
        using GaWeCodes.Thessera.Domain.Events;
        using GaWeCodes.Thessera.Domain.Naming;
        using Marten;
        using Microsoft.EntityFrameworkCore;

        namespace Snippet;

        public readonly record struct FirstId(Guid Value) : IEntityKey<Guid>
        {
            public bool IsEmpty => Value == Guid.Empty;
        }

        public sealed record FirstState(FirstId Id) : AggregateState<FirstState, FirstId>
        {
            public override FirstState Apply(IDomainEvent domainEvent) => this;
        }

        [AggregateName("first")]
        public sealed class First : AggregateRoot<FirstId, FirstState>
        {
            private First() : base(new FirstState(default)) { }
        }

        public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options);

        public sealed record SampleCommand : ICommand;
        """;

    [Fact]
    public async Task ACommandHandlerInjectingADbContext_IsFlagged()
    {
        var source = Prelude + """

            public sealed class SampleCommandHandler : ICommandHandler<SampleCommand>
            {
                public SampleCommandHandler(SampleDbContext context) { }

                public Task<Result> HandleAsync(SampleCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success());
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerRawSessionAnalyzer(),
            source,
            referenceStores: true);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(CommandHandlerRawSessionAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("SampleCommandHandler", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("SampleDbContext", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("EF Core", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommandHandlerInjectingAnIDocumentSession_IsFlagged()
    {
        var source = Prelude + """

            public sealed class SampleCommandHandler : ICommandHandler<SampleCommand>
            {
                public SampleCommandHandler(IDocumentSession session) { }

                public Task<Result> HandleAsync(SampleCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success());
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerRawSessionAnalyzer(),
            source,
            referenceStores: true);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(CommandHandlerRawSessionAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("SampleCommandHandler", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("IDocumentSession", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("Marten", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommandHandlerInjectingOnlyARepository_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed class SampleCommandHandler : ICommandHandler<SampleCommand>
            {
                public SampleCommandHandler(IRepository<First, FirstId> firsts) { }

                public Task<Result> HandleAsync(SampleCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success());
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerRawSessionAnalyzer(),
            source,
            referenceStores: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AQueryHandlerInjectingADbContext_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed record SampleQuery : IQuery<int>;

            public sealed class SampleQueryHandler : IQueryHandler<SampleQuery, int>
            {
                public SampleQueryHandler(SampleDbContext context) { }

                public Task<Result<int>> HandleAsync(SampleQuery query, CancellationToken cancellationToken) =>
                    Task.FromResult(Result<int>.Success(0));
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerRawSessionAnalyzer(),
            source,
            referenceStores: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ACommandHandlerInjectingAnIQuerySession_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed class SampleCommandHandler : ICommandHandler<SampleCommand>
            {
                public SampleCommandHandler(IQuerySession session) { }

                public Task<Result> HandleAsync(SampleCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success());
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerRawSessionAnalyzer(),
            source,
            referenceStores: true);

        Assert.Empty(diagnostics);
    }
}
