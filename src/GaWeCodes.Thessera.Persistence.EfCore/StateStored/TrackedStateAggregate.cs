using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed record TrackedStateAggregate(
    IDomainEventOwner Aggregate,
    IStateOwner StateOwner,
    object PersistedState,
    string AggregateName,
    string AggregateId) : ITrackedAggregate
{
    public long CurrentVersion => StateOwner.Version;
}
