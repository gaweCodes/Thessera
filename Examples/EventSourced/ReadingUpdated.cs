using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace EventSourced;

[EventName("reading-updated-v1")]
public sealed record ReadingUpdated(ReadingId ReadingId, int Value, DateTimeOffset OccurredAt)
    : DomainEvent, IRetiredDomainEvent<ReadingUpdatedV2>
{
    public ReadingUpdatedV2 Upcast() => new(ReadingId, NewValue: Value, OccurredAt);
}
