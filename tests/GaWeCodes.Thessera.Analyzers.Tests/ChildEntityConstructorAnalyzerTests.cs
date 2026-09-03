using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class ChildEntityConstructorAnalyzerTests
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

        public sealed record ChildState(ChildId Id) : EntityState<ChildState, ChildId>
        {
            public override ChildState Apply(IDomainEvent domainEvent) => this;
        }
        """;

    [Fact]
    public async Task AChildEntityWithAPublicConstructor_IsFlagged()
    {
        var source = Prelude + """

            public sealed class Child : Entity<ChildId, ChildState>
            {
                public Child(IChildOwner<ChildId, ChildState> owner, ChildId id) : base(owner, id) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new ChildEntityConstructorAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ChildEntityConstructorAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("Child", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChildEntityWithAnInternalConstructor_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed class Child : Entity<ChildId, ChildState>
            {
                internal Child(IChildOwner<ChildId, ChildState> owner, ChildId id) : base(owner, id) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new ChildEntityConstructorAnalyzer(), source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AChildEntityWithAProtectedConstructor_IsFlagged()
    {
        var source = Prelude + """

            public class Child : Entity<ChildId, ChildState>
            {
                protected Child(IChildOwner<ChildId, ChildState> owner, ChildId id) : base(owner, id) { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new ChildEntityConstructorAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ChildEntityConstructorAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("Child", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
