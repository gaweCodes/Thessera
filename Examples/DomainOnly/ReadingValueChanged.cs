using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace DomainOnly;

[EventName("reading-value-changed-v1")]
public sealed record ReadingValueChanged(ReadingId ReadingId, int Value) : DomainEvent;
