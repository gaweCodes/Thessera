using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class AggregateStateSelfBindingAnalyzerTests
{
    private const string Prelude = """
        using System;
        using GaWeCodes.Thessera.Domain.Aggregates;
        using GaWeCodes.Thessera.Domain.Entities;
        using GaWeCodes.Thessera.Domain.Events;

        namespace Snippet;

        public readonly record struct SampleId(Guid Value) : IEntityKey<Guid>
        {
            public bool IsEmpty => Value == Guid.Empty;
        }

        public sealed record OtherState(SampleId Id) : AggregateState<OtherState, SampleId>
        {
            public override OtherState Apply(IDomainEvent domainEvent) => this;
        }
        """;

    [Fact]
    public async Task AnAggregateStateNamingASiblingAsTSelf_IsFlagged()
    {
        var source = Prelude + """

            public sealed record SampleState(SampleId Id) : AggregateState<OtherState, SampleId>
            {
                public override OtherState Apply(IDomainEvent domainEvent) => new OtherState(Id);
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new AggregateStateSelfBindingAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AggregateStateSelfBindingAnalyzer.DiagnosticId, diagnostic.Id);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("SampleState", message, StringComparison.Ordinal);
        Assert.Contains("OtherState", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAggregateStateNamingItselfAsTSelf_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed record SampleState(SampleId Id) : AggregateState<SampleState, SampleId>
            {
                public override SampleState Apply(IDomainEvent domainEvent) => this;
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new AggregateStateSelfBindingAnalyzer(), source);

        Assert.Empty(diagnostics);
    }
}
