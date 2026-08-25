using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.ReadModels;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Persistence.Marten.ReadModels;

public sealed class EventSourcedReadModelRebuildRunner(IServiceScopeFactory scopeFactory)
{
    private readonly ReadModelRebuildWriter _writer = new(scopeFactory);

    public async Task RebuildAsync<TAggregate, TKey>(CancellationToken cancellationToken)
        where TAggregate : class, IEventSourcedAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        await _writer.ClearAsync<TAggregate, TKey>(cancellationToken).ConfigureAwait(false);

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var session = store.QuerySession();

        await using (session.ConfigureAwait(false))
        {
            await RebuildFromStreamsAsync<TAggregate, TKey>(session, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RebuildFromStreamsAsync<TAggregate, TKey>(
        IQuerySession session,
        CancellationToken cancellationToken)
        where TAggregate : class, IEventSourcedAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        var streamKeys = await StreamKeysAsync<TAggregate>(session, cancellationToken).ConfigureAwait(false);
        var batch = new List<TAggregate>(ReadModelRebuildWriter.BatchSize);

        foreach (var streamKey in streamKeys)
        {
            var stream = await session.Events
                .FetchStreamAsync(streamKey, token: cancellationToken)
                .ConfigureAwait(false);

            if (stream is not { Count: > 0 })
            {
                continue;
            }

            var aggregate = AggregateFactory.CreateEmpty<TAggregate>();
            ((IEventSourcedAggregateRoot<TKey>)aggregate)
                .LoadFromHistory(stream.Select(@event => (IDomainEvent)@event.Data));
            batch.Add(aggregate);

            if (batch.Count < ReadModelRebuildWriter.BatchSize)
            {
                continue;
            }

            await _writer.WriteAsync<TAggregate, TKey>(batch, cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            await _writer.WriteAsync<TAggregate, TKey>(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<string>> StreamKeysAsync<TAggregate>(
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var prefix = EntityKeyFormatter.GetStreamKeyPrefix(
            EntityKeyFormatter.GetAggregateName(typeof(TAggregate)));

        var keys = await session.Events.QueryAllRawEvents()
            .Where(@event => @event.StreamKey!.StartsWith(prefix))
            .Select(@event => @event.StreamKey!)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. keys.OrderBy(key => key, StringComparer.Ordinal)];
    }
}
