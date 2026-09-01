using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

/// <summary>
/// The serialized form a domain event travels and is stored in: the event itself as text, plus
/// everything needed to know where it came from.
/// </summary>
/// <param name="EventName">
/// The event's persisted name, from its <see cref="EventNameAttribute"/>. This is what the payload
/// is resolved back to a type by, which is why renaming the attribute value orphans everything
/// stored under the old one.
/// </param>
/// <param name="Payload">The serialized event.</param>
/// <param name="EventId">The event's identity, stable across redeliveries.</param>
/// <param name="AggregateName">
/// The aggregate's persisted name, from its <see cref="AggregateNameAttribute"/>.
/// </param>
/// <param name="AggregateId">The aggregate's identity, in the pinned stream-key format.</param>
/// <param name="Version">The aggregate version this event produced.</param>
/// <param name="OccurredAt">When the event happened.</param>
/// <remarks>
/// The metadata a handler receives is the in-process view of the same event; this is the
/// on-the-wire view. Whether one envelope per uncommitted event is written to an outbox in the
/// same transaction as the aggregate, and whether <c>AggregateName</c> and <c>AggregateId</c> form
/// the partition key of a durable queue, is runtime-dependent; see "What this package promises" in
/// the package README.
/// </remarks>
public sealed record DomainEventEnvelope(
    string EventName,
    string Payload,
    Guid EventId,
    string AggregateName,
    string AggregateId,
    long Version,
    DateTimeOffset OccurredAt);
