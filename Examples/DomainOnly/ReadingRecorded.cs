using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace DomainOnly;

[EventName("reading-recorded-v1")]
public sealed record ReadingRecorded(ReadingId ReadingId, int Value) : DomainEvent;
