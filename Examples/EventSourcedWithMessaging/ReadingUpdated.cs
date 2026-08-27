using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace EventSourcedWithMessaging;

[EventName("reading-updated-v1")]
public sealed record ReadingUpdated(ReadingId ReadingId, int Value, DateTimeOffset OccurredAt) : DomainEvent;
