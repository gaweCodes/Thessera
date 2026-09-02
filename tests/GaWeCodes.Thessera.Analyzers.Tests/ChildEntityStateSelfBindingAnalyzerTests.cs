using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class ChildEntityStateSelfBindingAnalyzerTests
{
    private const string Prelude = """
        using System;
        using GaWeCodes.Thessera.Domain.Entities;
        using GaWeCodes.Thessera.Domain.Events;

        namespace Snippet;

        public readonly record struct ChildId(Guid Value) : IEntityKey<Guid>
        {
            public bool IsEmpty => Value == Guid.Empty;
        }

        public sealed record OtherChildState(ChildId Id) : EntityState<OtherChildState, ChildId>
        {
            public override OtherChildState Apply(IDomainEvent domainEvent) => this;
        }
        """;

    [Fact]
    public async Task AChildEntityStateNamingASiblingAsTSelf_IsFlagged()
    {
        var source = Prelude + """

            public sealed record ChildState(ChildId Id) : EntityState<OtherChildState, ChildId>
            {
                public override OtherChildState Apply(IDomainEvent domainEvent) => new OtherChildState(Id);
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new ChildEntityStateSelfBindingAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ChildEntityStateSelfBindingAnalyzer.DiagnosticId, diagnostic.Id);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("ChildState", message, StringComparison.Ordinal);
        Assert.Contains("OtherChildState", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChildEntityStateNamingItselfAsTSelf_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed record ChildState(ChildId Id) : EntityState<ChildState, ChildId>
            {
                public override ChildState Apply(IDomainEvent domainEvent) => this;
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new ChildEntityStateSelfBindingAnalyzer(), source);

        Assert.Empty(diagnostics);
    }
}
