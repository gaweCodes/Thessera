using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Persistence.Marten;

internal sealed record TrackedAggregate(
    IDomainEventOwner Aggregate,
    string AggregateName,
    string AggregateId,
    Func<long> Version) : ITrackedAggregate
{
    public long CurrentVersion => Version();
}
