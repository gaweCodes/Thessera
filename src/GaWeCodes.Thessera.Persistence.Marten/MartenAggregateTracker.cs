using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Persistence.Marten;

internal sealed class MartenAggregateTracker : AggregateTracker<TrackedAggregate>
{
    public void Track(IDomainEventOwner aggregate, string aggregateName, string aggregateId, Func<long> version)
    {
        ArgumentNullException.ThrowIfNull(version);

        Add(new TrackedAggregate(aggregate, aggregateName, aggregateId, version));
    }
}
