namespace MixedPersistenceWithMessaging;

/// <summary>
/// A dedicated read side for <see cref="Reading"/>, kept separate from the Marten event store so
/// <see cref="ListReadingsHandler"/> never has to replay a stream to answer a query.
/// </summary>
public interface IReadingReadModelStore
{
    void Clear();

    void Upsert(ReadingSnapshot snapshot);

    IReadOnlyCollection<ReadingSnapshot> All();
}
