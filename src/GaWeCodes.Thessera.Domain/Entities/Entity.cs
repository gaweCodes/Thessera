using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Rules;

namespace GaWeCodes.Thessera.Domain.Entities;

public abstract class Entity<TKey, TState> : EntityBase<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
    where TState : EntityState<TState, TKey>
{
    private readonly IChildOwner<TKey, TState> _owner;

    protected Entity(IChildOwner<TKey, TState> owner, TKey id)
    {
        if (id.IsEmpty)
        {
            throw new DomainValidationException("The id of an entity cannot be empty.");
        }

        ArgumentNullException.ThrowIfNull(owner);

        Id = id;
        _owner = owner;
    }

    public sealed override TKey Id { get; }

    protected TState GetCurrentState()
    {
        return _owner.FindChild(Id)
            ?? throw new DomainValidationException(
                $"The entity '{Id}' is no longer part of '{_owner.GetType().Name}'.");
    }

    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _owner.Raise(domainEvent);
    }
}
