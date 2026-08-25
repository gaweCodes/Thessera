using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace GaWeCodes.Thessera.Persistence.EfCore.StateStored;

internal sealed class EfCoreUnitOfWork<TContext>(
    IDbContextOutbox<TContext> outbox,
    EfCoreAggregateTracker tracker,
    DomainEventEnvelopeFactory envelopeFactory) : IUnitOfWork
    where TContext : DbContext
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var entries = tracker.Entries;

        foreach (var entry in entries)
        {
            var tracked = outbox.DbContext.Entry(entry.PersistedState);

            AggregateStateGraph.Reconcile(tracked, entry.StateOwner.State);
        }

        foreach (var envelope in envelopeFactory.WrapUncommitted(entries))
        {
            await outbox.PublishAsync(envelope).ConfigureAwait(false);
        }

        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken).ConfigureAwait(false);

        tracker.ClearDomainEvents();
    }
}
