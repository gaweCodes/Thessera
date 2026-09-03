using System.Collections.Concurrent;

namespace MixedPersistence;

/// <summary>
/// An in-memory stand-in for a dedicated read database. It is deliberately the only thing
/// <see cref="ReadingReadModelRebuilder"/> writes to and <see cref="ListReadingsHandler"/> reads
/// from - the Marten event store underneath is never touched to answer a query.
/// </summary>
public sealed class ReadingReadModelStore : IReadingReadModelStore
{
    private readonly ConcurrentDictionary<int, ReadingSnapshot> _rows = new();

    public void Clear() => _rows.Clear();

    public void Upsert(ReadingSnapshot snapshot) => _rows[snapshot.Id] = snapshot;

    public IReadOnlyCollection<ReadingSnapshot> All() => [.. _rows.Values];
}
