using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregateNameAnalyzerTests
{
    private const string Prelude = """
        using System;
        using GaWeCodes.Thessera.Domain.Aggregates;
        using GaWeCodes.Thessera.Domain.Entities;
        using GaWeCodes.Thessera.Domain.Events;
        using GaWeCodes.Thessera.Domain.Naming;

        namespace Snippet;

        public readonly record struct SampleId(Guid Value) : IEntityKey<Guid>
        {
            public bool IsEmpty => Value == Guid.Empty;
        }

        public sealed record SampleState(SampleId Id) : AggregateState<SampleState, SampleId>
        {
            public override SampleState Apply(IDomainEvent domainEvent) => this;
        }
        """;

    [Fact]
    public async Task AnAggregateWithoutAggregateName_IsFlagged()
    {
        var source = Prelude + """

            public sealed class Sample : AggregateRoot<SampleId, SampleState>
            {
                private Sample(SampleState state) : base(state) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new AggregateNameAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AggregateNameAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("Sample", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAggregateWithAggregateName_IsNotFlagged()
    {
        var source = Prelude + """

            [AggregateName("sample")]
            public sealed class Sample : AggregateRoot<SampleId, SampleState>
            {
                private Sample(SampleState state) : base(state) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new AggregateNameAnalyzer(), source);

        Assert.Empty(diagnostics);
    }
}
