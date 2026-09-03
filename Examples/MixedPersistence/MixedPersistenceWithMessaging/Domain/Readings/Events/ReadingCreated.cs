using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistenceWithMessaging;

[EventName("reading-created-v1")]
public sealed record ReadingCreated(ReadingId ReadingId, int Value, DateTimeOffset OccurredAt) : DomainEvent;
