using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using Marten;

namespace GaWeCodes.Thessera.Persistence.Marten;

internal sealed class MartenEventSourcedRepository<TAggregate, TKey>(IDocumentSession session, MartenAggregateTracker tracker)
    : IRepository<TAggregate, TKey>
    where TAggregate : class, IEventSourcedAggregateRoot<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    public async Task<TAggregate?> GetByIdAsync(TKey id, CancellationToken cancellationToken)
    {
        if (id.IsEmpty)
        {
            return null;
        }

        var aggregateName = EntityKeyFormatter.GetAggregateName(typeof(TAggregate));
        var streamKey = EntityKeyFormatter.GetStreamKey(aggregateName, EntityKeyFormatter.GetKeyValue(id));
        var stream = await session.Events.FetchStreamAsync(streamKey, token: cancellationToken).ConfigureAwait(false);

        if (stream is not { Count: > 0 })
        {
            return null;
        }

        var aggregate = AggregateFactory.CreateEmpty<TAggregate>();
        ((IEventSourcedAggregateRoot<TKey>)aggregate).LoadFromHistory(stream.Select(@event => (IDomainEvent)@event.Data));
        Track(aggregate, aggregateName);
        return aggregate;
    }

    public Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        if (aggregate.Id.IsEmpty)
        {
            throw new InvalidOperationException(
                $"'{typeof(TAggregate)}' has no identity. An aggregate gains its identity through its first event; an empty hull exists only for rehydration.");
        }

        Track(aggregate, EntityKeyFormatter.GetAggregateName(typeof(TAggregate)));
        return Task.CompletedTask;
    }

    private void Track(TAggregate aggregate, string aggregateName)
    {
        tracker.Track(
            (IDomainEventOwner)aggregate,
            aggregateName,
            EntityKeyFormatter.GetKeyValue(aggregate.Id),
            () => ((IStateOwner)aggregate).Version);
    }
}
