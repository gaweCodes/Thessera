using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class EfCoreRepository<TAggregate, TKey>(
    WriteDbContextAccessor writeDbContext,
    EfCoreAggregateTracker tracker)
    : IRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    private readonly DbContext _context = writeDbContext?.Context
        ?? throw new ArgumentNullException(nameof(writeDbContext));

    public async Task<TAggregate?> GetByIdAsync(TKey id, CancellationToken cancellationToken)
    {
        if (id.IsEmpty)
        {
            return null;
        }

        var aggregate = CreateEmpty(out var stateOwner);

        var state = await _context
            .FindAsync(stateOwner.StateType, [id], cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            return null;
        }

        stateOwner.Restore(state);
        Track(aggregate, stateOwner, state);
        return aggregate;
    }

    public Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ThrowIfEmptyIdentity(aggregate);

        var stateOwner = AsStateOwner(aggregate);
        var state = stateOwner.State;

        AggregateStateGraph.EnsureTrackableCollections(_context, state);
        _context.Add(state);
        Track(aggregate, stateOwner, state);
        return Task.CompletedTask;
    }

    private void Track(TAggregate aggregate, IStateOwner stateOwner, object state) =>
        tracker.Track(
            (IDomainEventOwner)aggregate,
            stateOwner,
            state,
            EntityKeyFormatter.GetAggregateName(typeof(TAggregate)),
            EntityKeyFormatter.GetKeyValue(aggregate.Id));

    private static void ThrowIfEmptyIdentity(TAggregate aggregate)
    {
        if (aggregate.Id.IsEmpty)
        {
            throw new InvalidOperationException(
                $"'{typeof(TAggregate)}' has no identity. An aggregate gains its identity through its first event; an empty hull exists only for rehydration.");
        }
    }

    private static TAggregate CreateEmpty(out IStateOwner stateOwner)
    {
        var aggregate = AggregateFactory.CreateEmpty<TAggregate>();

        stateOwner = AsStateOwner(aggregate);
        return aggregate;
    }

    private static IStateOwner AsStateOwner(TAggregate aggregate) =>
        aggregate as IStateOwner
        ?? throw new InvalidOperationException(
            $"The aggregate '{typeof(TAggregate)}' does not expose its state and cannot be persisted by EF Core.");
}
