using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class CommandHandlerSingleStoreAnalyzerTests
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

        namespace Snippet;

        public readonly record struct FirstId(Guid Value) : IEntityKey<Guid>
        {
            public bool IsEmpty => Value == Guid.Empty;
        }

        public readonly record struct SecondId(Guid Value) : IEntityKey<Guid>
        {
            public bool IsEmpty => Value == Guid.Empty;
        }

        public sealed record FirstState(FirstId Id) : AggregateState<FirstState, FirstId>
        {
            public override FirstState Apply(IDomainEvent domainEvent) => this;
        }

        public sealed record SecondState(SecondId Id) : AggregateState<SecondState, SecondId>
        {
            public override SecondState Apply(IDomainEvent domainEvent) => this;
        }

        [AggregateName("first")]
        public sealed class First : AggregateRoot<FirstId, FirstState>
        {
            private First() : base(new FirstState(default)) { }
        }

        [AggregateName("second")]
        public sealed class Second : AggregateRoot<SecondId, SecondState>
        {
            private Second() : base(new SecondState(default)) { }
        }

        public sealed record SampleCommand : ICommand;
        """;

    [Fact]
    public async Task ACommandHandlerInjectingRepositoriesForTwoAggregates_IsFlagged()
    {
        var source = Prelude + """

            public sealed class SampleCommandHandler : ICommandHandler<SampleCommand>
            {
                public SampleCommandHandler(IRepository<First, FirstId> firsts, IRepository<Second, SecondId> seconds) { }

                public Task<Result> HandleAsync(SampleCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success());
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerSingleStoreAnalyzer(),
            source,
            referenceApplication: true);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(CommandHandlerSingleStoreAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("First", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("Second", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommandHandlerInjectingOneRepositoryTwice_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed class SampleCommandHandler : ICommandHandler<SampleCommand>
            {
                public SampleCommandHandler(IRepository<First, FirstId> firsts, IRepository<First, FirstId> firstsAgain) { }

                public Task<Result> HandleAsync(SampleCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success());
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerSingleStoreAnalyzer(),
            source,
            referenceApplication: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ACommandHandlerInjectingOneAggregateRepository_IsNotFlagged()
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
            new CommandHandlerSingleStoreAnalyzer(),
            source,
            referenceApplication: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AClassThatIsNotACommandHandler_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed class NotAHandler
            {
                public NotAHandler(IRepository<First, FirstId> firsts, IRepository<Second, SecondId> seconds) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new CommandHandlerSingleStoreAnalyzer(),
            source,
            referenceApplication: true);

        Assert.Empty(diagnostics);
    }
}
