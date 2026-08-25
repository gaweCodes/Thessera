using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Tests.TestDoubles;

internal sealed record TestState(TestId Id, int Value) : AggregateState<TestState, TestId>
{
    public static TestState Empty => new(TestId.Empty, 0);

    public override TestState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        TestDomainEvent e => this with { Id = new TestId(e.NewValue), Value = e.NewValue },
        RawDomainEvent e => this with { Id = new TestId(e.NewValue), Value = e.NewValue },
        _ => this,
    };
}

internal sealed record NeverIdentifiedState(TestId Id, int Value) : AggregateState<NeverIdentifiedState, TestId>
{
    public static NeverIdentifiedState Empty => new(TestId.Empty, 0);

    public override NeverIdentifiedState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        TestDomainEvent e => this with { Value = e.NewValue },
        _ => this,
    };
}

internal sealed record NullApplyState(TestId Id, int Value) : AggregateState<NullApplyState, TestId>
{
    public static NullApplyState Empty => new(TestId.Empty, 0);

    public override NullApplyState Apply(IDomainEvent domainEvent) => null!;
}
