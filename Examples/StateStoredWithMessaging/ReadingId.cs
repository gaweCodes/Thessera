using GaWeCodes.Thessera.Domain.Entities;

namespace StateStoredWithMessaging;

public readonly record struct ReadingId(int Value) : IEntityKey<int>
{
    public bool IsEmpty => Value <= 0;
}
