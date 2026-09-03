using GaWeCodes.Thessera.Domain.Entities;

namespace MixedPersistence;

public readonly record struct AccountId(int Value) : IEntityKey<int>
{
    public bool IsEmpty => Value <= 0;
}
