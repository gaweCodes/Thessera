namespace StateStored;

/// <summary>
/// A dedicated read side for <see cref="Reading"/>, kept separate from <see cref="ReadingDbContext"/>
/// so <see cref="ListReadingsHandler"/> never has to query the write table to answer a query.
/// </summary>
public interface IReadingReadModelStore
{
    void Clear();

    void Upsert(ReadingSnapshot snapshot);

    IReadOnlyCollection<ReadingSnapshot> All();
}
