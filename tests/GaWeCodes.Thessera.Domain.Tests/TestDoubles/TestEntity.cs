using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Tests.TestDoubles;

internal sealed class NoOpRaiser : IDomainEventRaiser
{
    public void Raise(IDomainEvent domainEvent)
    {
    }
}

internal sealed class StubChildOwner(TestId id) : IChildOwner<TestId, ChildState>
{
    public void Raise(IDomainEvent domainEvent)
    {
    }

    public ChildState? FindChild(TestId childId) => new(id, 0);
}

internal sealed class TestEntity(TestId id)
    : Entity<TestId, ChildState>(new StubChildOwner(id), id);

internal sealed class OtherTestEntity(TestId id)
    : Entity<TestId, ChildState>(new StubChildOwner(id), id);
