using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace HullFixture;

public readonly record struct ExposedChildId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;
}

public sealed record ExposedChildState(ExposedChildId Id) : EntityState<ExposedChildState, ExposedChildId>
{
    public override ExposedChildState Apply(IDomainEvent domainEvent) => this;
}

public sealed class ExposedChild : Entity<ExposedChildId, ExposedChildState>
{
    public ExposedChild(IChildOwner<ExposedChildId, ExposedChildState> owner, ExposedChildId id)
        : base(owner, id)
    {
    }
}
