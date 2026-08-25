using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Tests.TestDoubles;

internal sealed class TestEventSourcedAggregate()
    : EventSourcedAggregateRoot<TestId, TestState>(TestState.Empty)
{
    public TestState CurrentState => State;

    public void Raise(IDomainEvent domainEvent) => RaiseEvent(domainEvent);
}

internal sealed class OtherEventSourcedAggregate()
    : EventSourcedAggregateRoot<TestId, TestState>(TestState.Empty)
{
    public void Raise(IDomainEvent domainEvent) => RaiseEvent(domainEvent);
}

internal sealed class NullApplyAggregate() : AggregateRoot<TestId, NullApplyState>(NullApplyState.Empty)
{
    public void Raise(IDomainEvent domainEvent) => RaiseEvent(domainEvent);
}

internal sealed class NullApplyEventSourcedAggregate()
    : EventSourcedAggregateRoot<TestId, NullApplyState>(NullApplyState.Empty);
