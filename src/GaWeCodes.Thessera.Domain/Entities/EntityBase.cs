namespace GaWeCodes.Thessera.Domain.Entities;

/// <summary>
/// The identity-based equality every entity and aggregate root in the family shares.
/// </summary>
/// <typeparam name="TKey">The typed identity.</typeparam>
/// <remarks>
/// Two entities are the same when they are of the same type and carry the same identity — never
/// because their data happens to match. That is the difference between an entity and a value: a
/// reading whose value was corrected is still the same reading.
/// <para>
/// The constructor is deliberately not public: entities are created by their aggregate, and
/// aggregates by their own named factory methods.
/// </para>
/// </remarks>
public abstract class EntityBase<TKey> : IEntity<TKey>, IEquatable<EntityBase<TKey>>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    private protected EntityBase()
    {
    }

    /// <summary>
    /// Gets the identity this entity is compared by.
    /// </summary>
    public abstract TKey Id { get; }

    /// <summary>
    /// Determines whether <paramref name="other"/> is the same entity: same runtime type, same
    /// identity.
    /// </summary>
    /// <param name="other">The entity to compare with, possibly <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when both are of the same runtime type and their identities are
    /// equal; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The runtime type is part of the comparison, so two entities of different types never compare
    /// equal even if they share a key value.
    /// </remarks>
    public bool Equals(EntityBase<TKey>? other)
    {
        return other is not null
               && other.GetType() == GetType()
               && Id.Equals(other.Id);
    }

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj)
    {
        return Equals(obj as EntityBase<TKey>);
    }

    /// <inheritdoc/>
    public sealed override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }

    /// <summary>
    /// Determines whether two entities are the same entity.
    /// </summary>
    /// <param name="left">The first entity, possibly <see langword="null"/>.</param>
    /// <param name="right">The second entity, possibly <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are <see langword="null"/> or the same entity.</returns>
    public static bool operator ==(EntityBase<TKey>? left, EntityBase<TKey>? right)
    {
        return ReferenceEquals(left, right) || (left is not null && right is not null && left.Equals(right));
    }

    /// <summary>
    /// Determines whether two entities are different entities.
    /// </summary>
    /// <param name="left">The first entity, possibly <see langword="null"/>.</param>
    /// <param name="right">The second entity, possibly <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when they are not the same entity.</returns>
    public static bool operator !=(EntityBase<TKey>? left, EntityBase<TKey>? right)
    {
        return !(left == right);
    }
}
