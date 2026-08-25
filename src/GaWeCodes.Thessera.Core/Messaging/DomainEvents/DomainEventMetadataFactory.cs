using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

public static class DomainEventMetadataFactory
{
    public static DomainEventMetadata From(DomainEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new DomainEventMetadata(
            envelope.EventId,
            envelope.AggregateName,
            envelope.AggregateId,
            envelope.Version,
            envelope.OccurredAt);
    }
}
