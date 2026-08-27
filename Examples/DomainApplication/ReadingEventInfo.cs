using GaWeCodes.Thessera.Domain.Events;

namespace DomainApplication;

public sealed record ReadingEventInfo(string Type, int ReadingId, int? Value, DateTimeOffset OccurredAt)
{
    public static ReadingEventInfo From(IDomainEvent domainEvent) => domainEvent switch
    {
        ReadingCreated created => new(nameof(ReadingCreated), created.ReadingId.Value, created.Value, created.OccurredAt),
        ReadingUpdated updated => new(nameof(ReadingUpdated), updated.ReadingId.Value, updated.Value, updated.OccurredAt),
        ReadingDeleted deleted => new(nameof(ReadingDeleted), deleted.ReadingId.Value, null, deleted.OccurredAt),
        _ => throw new InvalidOperationException($"Unknown domain event '{domainEvent.GetType().Name}'."),
    };
}
