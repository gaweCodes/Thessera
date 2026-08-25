namespace GaWeCodes.Thessera.Domain.Entities;

public abstract class EntityBase<TKey> : IEntity<TKey>, IEquatable<EntityBase<TKey>>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    private protected EntityBase()
    {
    }

    public abstract TKey Id { get; }

    public bool Equals(EntityBase<TKey>? other)
    {
        return other is not null
               && other.GetType() == GetType()
               && Id.Equals(other.Id);
    }

    public sealed override bool Equals(object? obj)
    {
        return Equals(obj as EntityBase<TKey>);
    }

    public sealed override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(EntityBase<TKey>? left, EntityBase<TKey>? right)
    {
        return ReferenceEquals(left, right) || (left is not null && right is not null && left.Equals(right));
    }

    public static bool operator !=(EntityBase<TKey>? left, EntityBase<TKey>? right)
    {
        return !(left == right);
    }
}
