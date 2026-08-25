using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Domain;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Persistence;

public sealed class DomainEventEnvelopeFactory(DomainEventEnvelopeSerializer serializer, IClock clock)
{
    public IReadOnlyList<DomainEventEnvelope> WrapUncommitted(IEnumerable<ITrackedAggregate> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var occurredAt = clock.Now;
        var envelopes = new List<DomainEventEnvelope>();

        foreach (var entry in entries)
        {
            var domainEvents = entry.Aggregate.DomainEvents;
            var version = entry.CurrentVersion - domainEvents.Count;

            foreach (var domainEvent in domainEvents)
            {
                envelopes.Add(serializer.Wrap(
                    domainEvent,
                    Guid.NewGuid(),
                    entry.AggregateName,
                    entry.AggregateId,
                    ++version,
                    occurredAt));
            }
        }

        return envelopes;
    }
}
