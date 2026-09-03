using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.ReadModels;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Persistence.Marten.ReadModels;

/// <summary>
/// Rebuilds a read model by replaying what actually happened: it walks the streams of one aggregate
/// type, replays each one, and hands the rebuilt aggregate to your rebuilder.
/// </summary>
/// <param name="scopeFactory">
/// Held open for the whole run while reading; the writer this creates opens its own scope per batch.
/// </param>
/// <remarks>
/// Registered for you by <c>UseMartenEventStore</c>; resolve it when a projection changed and its
/// read model has to catch up. Unlike the state-stored runner this sees the history, so the rebuilt
/// model reflects the events rather than only the current state.
/// </remarks>
public sealed class EventSourcedReadModelRebuildRunner(IServiceScopeFactory scopeFactory)
{
    private readonly ReadModelRebuildWriter _writer = new(scopeFactory);

    /// <summary>
    /// Clears the read model and rebuilds it from every stream of this aggregate type.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate whose streams are replayed.</typeparam>
    /// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <returns>A task that completes once every stream has been replayed and written.</returns>
    /// <remarks>
    /// The read model is empty from the moment it is cleared until the rebuild finishes, so run this
    /// where that is acceptable. Your <c>IReadModelRebuilder</c> for this aggregate has to be
    /// registered, and it is what decides what the rebuilt model looks like.
    /// </remarks>
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
