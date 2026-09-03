namespace MixedPersistenceWithMessaging;

public sealed record ReadingListResponse(
    string Operation,
    IReadOnlyList<ReadingSnapshot> Readings,
    IReadOnlyList<ReadingEventInfo> DomainEvents);
