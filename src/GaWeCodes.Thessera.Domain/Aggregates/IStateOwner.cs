namespace GaWeCodes.Thessera.Domain.Aggregates;

public interface IStateOwner
{
    Type StateType { get; }

    object State { get; }

    long Version { get; }

    void Restore(object state);
}
