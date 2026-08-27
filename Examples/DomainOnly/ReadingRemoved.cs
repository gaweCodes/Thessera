using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace DomainOnly;

[EventName("reading-removed-v1")]
public sealed record ReadingRemoved(ReadingId ReadingId) : DomainEvent;
