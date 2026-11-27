using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Tests.TestDoubles;

internal sealed record TestDomainEvent(int NewValue) : DomainEvent;

internal sealed record RetiredTestDomainEvent(int Value) : DomainEvent, IRetiredDomainEvent<TestDomainEvent>
{
    public TestDomainEvent Upcast() => new(Value);
}

internal sealed class RawDomainEvent(int newValue) : IDomainEvent
{
    public int NewValue { get; } = newValue;
}

internal sealed record IgnoredDomainEvent : DomainEvent;
