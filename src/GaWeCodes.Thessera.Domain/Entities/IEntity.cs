namespace GaWeCodes.Thessera.Domain.Entities;

/// <summary>
/// Something with an identity, compared by that identity rather than by its data.
/// </summary>
/// <typeparam name="TKey">The typed identity.</typeparam>
public interface IEntity<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    /// <summary>
    /// Gets the identity that distinguishes this entity from every other of its type.
    /// </summary>
    TKey Id { get; }
}
