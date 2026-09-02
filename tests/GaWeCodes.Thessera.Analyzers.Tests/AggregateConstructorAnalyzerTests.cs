using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregateConstructorAnalyzerTests
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
    public async Task AnAggregateWithoutAParameterlessConstructor_IsFlagged()
    {
        var source = Prelude + """

            [AggregateName("sample")]
            public sealed class Sample : AggregateRoot<SampleId, SampleState>
            {
                private Sample(SampleState state) : base(state) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new AggregateConstructorAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AggregateConstructorAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("no parameterless constructor", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAggregateWithAPublicParameterlessConstructor_IsFlagged()
    {
        var source = Prelude + """

            [AggregateName("sample")]
            public sealed class Sample : AggregateRoot<SampleId, SampleState>
            {
                public Sample() : base(new SampleState(default)) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new AggregateConstructorAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AggregateConstructorAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("public parameterless constructor", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAggregateWithAPrivateParameterlessConstructor_IsNotFlagged()
    {
        var source = Prelude + """

            [AggregateName("sample")]
            public sealed class Sample : AggregateRoot<SampleId, SampleState>
            {
                private Sample() : base(new SampleState(default)) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new AggregateConstructorAnalyzer(), source);

        Assert.Empty(diagnostics);
    }
}
