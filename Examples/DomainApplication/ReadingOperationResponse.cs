namespace DomainApplication;

public sealed record ReadingOperationResponse(
    string Operation,
    ReadingSnapshot Reading,
    IReadOnlyList<ReadingEventInfo> DomainEvents);
