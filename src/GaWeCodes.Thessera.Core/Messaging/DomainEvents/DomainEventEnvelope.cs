using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

public sealed record DomainEventEnvelope(
    string EventName,
    string Payload,
    Guid EventId,
    string AggregateName,
    string AggregateId,
    long Version,
    DateTimeOffset OccurredAt);
