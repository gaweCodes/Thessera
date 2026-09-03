using GaWeCodes.Thessera.Application.ReadModels;

namespace EventSourced;

/// <summary>
/// Projects a replayed <see cref="Reading"/> into <see cref="IReadingReadModelStore"/>. Discovered
/// and registered automatically because it implements <see cref="IReadModelRebuilder{TAggregate, TKey}"/>;
/// <c>EventSourcedReadModelRebuildRunner</c> is what drives it.
/// </summary>
public sealed class ReadingReadModelRebuilder(IReadingReadModelStore store) : IReadModelRebuilder<Reading, ReadingId>
{
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        store.Clear();
        return Task.CompletedTask;
    }

    public Task RebuildAsync(Reading aggregate, CancellationToken cancellationToken)
    {
        store.Upsert(ReadingSnapshot.From(aggregate));
        return Task.CompletedTask;
    }
}
