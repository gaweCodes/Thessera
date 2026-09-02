using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class DomainEventNameAnalyzerTests
{
    [Fact]
    public async Task ADomainEventWithoutEventName_IsFlagged()
    {
        const string source = """
            using System;
            using GaWeCodes.Thessera.Domain.Entities;
            using GaWeCodes.Thessera.Domain.Events;

            namespace Snippet;

            public readonly record struct SampleId(Guid Value) : IEntityKey<Guid>
            {
                public bool IsEmpty => Value == Guid.Empty;
            }

            public sealed record SampleCreated(SampleId Id) : DomainEvent;
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new DomainEventNameAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DomainEventNameAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("SampleCreated", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADomainEventWithEventName_IsNotFlagged()
    {
        const string source = """
            using System;
            using GaWeCodes.Thessera.Domain.Entities;
            using GaWeCodes.Thessera.Domain.Events;
            using GaWeCodes.Thessera.Domain.Naming;

            namespace Snippet;

            public readonly record struct SampleId(Guid Value) : IEntityKey<Guid>
            {
                public bool IsEmpty => Value == Guid.Empty;
            }

            [EventName("sample-created-v1")]
            public sealed record SampleCreated(SampleId Id) : DomainEvent;
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new DomainEventNameAnalyzer(), source);

        Assert.Empty(diagnostics);
    }
}
