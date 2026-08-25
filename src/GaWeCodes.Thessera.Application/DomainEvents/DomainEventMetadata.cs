using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Application.DomainEvents;

public sealed record DomainEventMetadata(
    Guid EventId,
    string AggregateName,
    string AggregateId,
    long Version,
    DateTimeOffset OccurredAt);
