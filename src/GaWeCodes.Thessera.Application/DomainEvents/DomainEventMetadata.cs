using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Application.DomainEvents;

/// <summary>
/// The context that travels with a domain event when it is handled rather than raised: who it came
/// from, when, and where in that aggregate's history it sits.
/// </summary>
/// <param name="EventId">
/// The identity of this event. Stable across redeliveries, so a handler can recognise one it has
/// already seen.
/// </param>
/// <param name="AggregateName">
/// The aggregate's persisted name, from its <see cref="AggregateNameAttribute"/>.
/// </param>
/// <param name="AggregateId">
/// The aggregate's identity, rendered in the same pinned format the stream key uses.
/// </param>
/// <param name="Version">
/// The aggregate version this event produced. Use it as a watermark: ignore anything not newer than
/// what the read model already holds, and a redelivered event cannot move it backwards.
/// </param>
/// <param name="OccurredAt">When the event happened, taken from the clock at commit time.</param>
/// <remarks>
/// Delivery is at-least-once, so a projection or a mapper may see the same event more than once.
/// <paramref name="Version"/> is the intended way to make that harmless.
/// </remarks>
public sealed record DomainEventMetadata(
    Guid EventId,
    string AggregateName,
    string AggregateId,
    long Version,
    DateTimeOffset OccurredAt);
