using System.Globalization;
using GaWeCodes.Thessera.Analyzers;

namespace GaWeCodes.Thessera.Tests;

public sealed class RetiredDomainEventRaisedAnalyzerTests
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

        [EventName("sample-renamed-v2")]
        public sealed record SampleRenamedV2(string NewName) : DomainEvent;

        [EventName("sample-renamed-v1")]
        public sealed record SampleRenamed(string Name) : DomainEvent, IRetiredDomainEvent<SampleRenamedV2>
        {
            public SampleRenamedV2 Upcast() => new(Name);
        }
        """;

    [Fact]
    public async Task RaisingARetiredEvent_IsFlagged()
    {
        var source = Prelude + """

            public sealed class Sample : AggregateRoot<SampleId, SampleState>
            {
                private Sample(SampleState state) : base(state) { }

                public void Rename(string name) => RaiseEvent(new SampleRenamed(name));
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetiredDomainEventRaisedAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(RetiredDomainEventRaisedAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("SampleRenamed", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RaisingARetiredEventFromAChildEntity_IsFlagged()
    {
        var source = Prelude + """

            public sealed record ChildState(SampleId Id) : EntityState<ChildState, SampleId>
            {
                public override ChildState Apply(IDomainEvent domainEvent) => this;
            }

            public sealed class Child : Entity<SampleId, ChildState>
            {
                internal Child(IChildOwner<SampleId, ChildState> owner, SampleId id) : base(owner, id) { }

                public void Rename(string name) => RaiseEvent(new SampleRenamed(name));
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetiredDomainEventRaisedAnalyzer(), source);

        Assert.Equal(RetiredDomainEventRaisedAnalyzer.DiagnosticId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task RaisingTheSuccessorOrCreatingARetiredEvent_IsNotFlagged()
    {
        var source = Prelude + """

            public sealed class Sample : AggregateRoot<SampleId, SampleState>
            {
                private Sample(SampleState state) : base(state) { }

                public void Rename(string name) => RaiseEvent(new SampleRenamed(name).Upcast());
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(new RetiredDomainEventRaisedAnalyzer(), source);

        Assert.Empty(diagnostics);
    }
}
