using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace EventSourced;

[EventName("reading-updated-v2")]
public sealed record ReadingUpdatedV2(ReadingId ReadingId, int NewValue, DateTimeOffset OccurredAt) : DomainEvent;
