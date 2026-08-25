namespace GaWeCodes.Thessera.Domain.Entities;

public interface IEntity<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    TKey Id { get; }
}
