using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Entities;

/// <summary>
/// The immutable record holding a child entity's data, folded by the same events that fold the
/// aggregate's own state.
/// </summary>
/// <typeparam name="TSelf">The deriving record itself.</typeparam>
/// <typeparam name="TKey">The child's typed identity.</typeparam>
/// <remarks>
/// Unlike <see cref="Aggregates.AggregateState{TSelf, TKey}"/> this carries no version: a child is
/// versioned by the aggregate it belongs to, because that aggregate is the unit that is committed.
/// On the EF Core-backed state store a child collection maps as an owned type, keyed by this
/// identity; other stores are free to map it differently.
/// </remarks>
public abstract record EntityState<TSelf, TKey>
    where TSelf : EntityState<TSelf, TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    /// <summary>
    /// Gets the child's identity, unique within its aggregate.
    /// </summary>
    public abstract TKey Id { get; init; }

    /// <summary>
    /// Returns the state that follows <paramref name="domainEvent"/>.
    /// </summary>
    /// <param name="domainEvent">The event to fold in.</param>
    /// <returns>
    /// A new state for an event this type knows, and <see langword="this"/> unchanged for one it
    /// does not.
    /// </returns>
    public abstract TSelf Apply(IDomainEvent domainEvent);
}
