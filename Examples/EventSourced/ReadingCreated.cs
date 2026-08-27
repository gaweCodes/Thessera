using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace EventSourced;

[EventName("reading-created-v1")]
public sealed record ReadingCreated(ReadingId ReadingId, int Value, DateTimeOffset OccurredAt) : DomainEvent;
