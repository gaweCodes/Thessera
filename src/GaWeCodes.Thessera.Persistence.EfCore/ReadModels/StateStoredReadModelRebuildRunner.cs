using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.ReadModels;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Persistence.EfCore.ReadModels;

/// <summary>
/// Rebuilds a read model from the stored state: it reads the write database in batches, rehydrates
/// each aggregate, and hands it to your rebuilder.
/// </summary>
/// <typeparam name="TContext">The write context the state is read from.</typeparam>
/// <param name="scopeFactory">Used to open a scope per batch, so a rebuild does not hold one open.</param>
/// <remarks>
/// Registered for you by <c>UseEfCoreStateStore</c>; resolve it when a projection changed and its
/// read model has to catch up.
/// <para>
/// A state store keeps no history, so a rebuild reconstructs the read model from current state
/// only. Anything a projection derived from the <em>sequence</em> of events — a count of how often
/// something happened, an intermediate value — cannot be recovered this way. On the event store the
/// equivalent runner replays the streams instead.
/// </para>
/// </remarks>
public sealed class StateStoredReadModelRebuildRunner<TContext>(IServiceScopeFactory scopeFactory)
    where TContext : DbContext
{
    private readonly ReadModelRebuildWriter _writer = new(scopeFactory);

    /// <summary>
    /// Clears the read model and rebuilds it from every stored state of this aggregate type.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate to rebuild.</typeparam>
    /// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
    /// <typeparam name="TState">
    /// The aggregate's state record — the type the context actually maps, since EF Core sees the
    /// state and not the aggregate.
    /// </typeparam>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <returns>A task that completes once every stored state has been written.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TAggregate"/> does not expose its state, so it cannot be rehydrated from
    /// the write database.
    /// </exception>
    /// <remarks>
    /// The read model is empty from the moment it is cleared until the rebuild finishes, so run this
    /// where that is acceptable. Your <c>IReadModelRebuilder</c> for this aggregate has to be
    /// registered.
    /// </remarks>
    public async Task RebuildAsync<TAggregate, TKey, TState>(CancellationToken cancellationToken)
        where TAggregate : class, IAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
        where TState : class
    {
        await _writer.ClearAsync<TAggregate, TKey>(cancellationToken).ConfigureAwait(false);

        using var readScope = scopeFactory.CreateScope();
        var context = readScope.ServiceProvider.GetRequiredService<TContext>();

        var batch = new List<TAggregate>(ReadModelRebuildWriter.BatchSize);

        await foreach (var state in context.Set<TState>()
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            batch.Add(Rehydrate<TAggregate>(state));

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

    private static TAggregate Rehydrate<TAggregate>(object state)
        where TAggregate : class
    {
        var aggregate = AggregateFactory.CreateEmpty<TAggregate>();

        if (aggregate is not IStateOwner stateOwner)
        {
            throw new InvalidOperationException(
                $"The aggregate '{typeof(TAggregate)}' does not expose its state and cannot be rebuilt from the write database.");
        }

        stateOwner.Restore(state);
        return aggregate;
    }
}
