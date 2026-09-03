using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace MixedPersistence;

[EventName("reading-deleted-v1")]
public sealed record ReadingDeleted(ReadingId ReadingId, DateTimeOffset OccurredAt) : DomainEvent;
