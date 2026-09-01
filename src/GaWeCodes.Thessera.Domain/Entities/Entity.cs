using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Rules;

namespace GaWeCodes.Thessera.Domain.Entities;

/// <summary>
/// A child entity: it has its own identity and its own behaviour, but no state and no event list of
/// its own — both live on the aggregate root it is reached through.
/// </summary>
/// <typeparam name="TKey">The child's typed identity.</typeparam>
/// <typeparam name="TState">The child's state record.</typeparam>
/// <remarks>
/// Keep the constructor of a deriving type <see langword="internal"/>. A child built without its
/// root would have no channel to raise events into and no state to read, and the failure would
/// appear only when someone used it. The convention check in <c>GaWeCodes.Thessera.Testing</c>
/// verifies this in a test rather than at run time.
/// </remarks>
public abstract class Entity<TKey, TState> : EntityBase<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
    where TState : EntityState<TState, TKey>
{
    private readonly IChildOwner<TKey, TState> _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity{TKey, TState}"/> class, bound to the
    /// aggregate that owns it.
    /// </summary>
    /// <param name="owner">The aggregate root this child belongs to.</param>
    /// <param name="id">The child's identity. Must not be empty.</param>
    /// <exception cref="DomainValidationException">
    /// <paramref name="id"/> is empty, so the child could not be found again inside its aggregate.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
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

    /// <inheritdoc/>
    public sealed override TKey Id { get; }

    /// <summary>
    /// Reads this child's state as it currently stands on the aggregate.
    /// </summary>
    /// <returns>The child's current state record.</returns>
    /// <exception cref="DomainValidationException">
    /// The aggregate no longer holds a child with this identity — an applied event removed it, and
    /// this instance is a stale handle.
    /// </exception>
    /// <remarks>
    /// Read the state each time rather than caching it. The aggregate replaces its state record on
    /// every applied event, so a cached copy is the state as it was, not as it is.
    /// </remarks>
    protected TState GetCurrentState()
    {
        return _owner.FindChild(Id)
            ?? throw new DomainValidationException(
                $"The entity '{Id}' is no longer part of '{_owner.GetType().Name}'.");
    }

    /// <summary>
    /// Raises a domain event through the aggregate root, which applies it and records it as
    /// uncommitted.
    /// </summary>
    /// <param name="domainEvent">The event to raise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _owner.Raise(domainEvent);
    }
}
