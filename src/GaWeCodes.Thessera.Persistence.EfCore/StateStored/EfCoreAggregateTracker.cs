using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class EfCoreAggregateTracker : AggregateTracker<TrackedStateAggregate>
{
    public void Track(
        IDomainEventOwner aggregate,
        IStateOwner stateOwner,
        object persistedState,
        string aggregateName,
        string aggregateId)
    {
        ArgumentNullException.ThrowIfNull(stateOwner);
        ArgumentNullException.ThrowIfNull(persistedState);

        Add(new TrackedStateAggregate(aggregate, stateOwner, persistedState, aggregateName, aggregateId));
    }
}
