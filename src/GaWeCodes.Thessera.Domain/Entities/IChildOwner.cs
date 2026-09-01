using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Entities;

/// <summary>
/// The aggregate root seen from one of its children: where the child finds its own state, and the
/// channel it raises events through.
/// </summary>
/// <typeparam name="TChildKey">The child's typed identity.</typeparam>
/// <typeparam name="TChildState">The child's state record.</typeparam>
/// <remarks>
/// A child holds no state and no event list of its own. Both live on the root, which is what keeps
/// the aggregate a single unit of change even when it is addressed through one of its parts.
/// </remarks>
public interface IChildOwner<TChildKey, TChildState> : IDomainEventRaiser
    where TChildKey : struct, IEntityKey, IEquatable<TChildKey>
    where TChildState : EntityState<TChildState, TChildKey>
{
    /// <summary>
    /// Looks up the current state of one child.
    /// </summary>
    /// <param name="childId">The child's identity.</param>
    /// <returns>
    /// The child's state, or <see langword="null"/> if the aggregate no longer holds a child with
    /// that identity — for example because an applied event removed it.
    /// </returns>
    TChildState? FindChild(TChildKey childId);
}
