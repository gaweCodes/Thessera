using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.ReadModels;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Persistence.EfCore.ReadModels;

public sealed class StateStoredReadModelRebuildRunner<TContext>(IServiceScopeFactory scopeFactory)
    where TContext : DbContext
{
    private readonly ReadModelRebuildWriter _writer = new(scopeFactory);

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
