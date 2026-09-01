using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Domain;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Turns the uncommitted events of every tracked aggregate into envelopes ready to be persisted.
/// </summary>
/// <param name="serializer">Serializes each event and resolves its persisted name.</param>
/// <param name="clock">Stamps the events, once per commit rather than once per event.</param>
/// <remarks>
/// A store calls this inside its unit of work, between reconciling the aggregate and saving.
/// Whether the envelopes end up in an outbox written in the same transaction as the aggregate is
/// runtime-dependent; see "What this package promises" in the package README.
/// </remarks>
public sealed class DomainEventEnvelopeFactory(DomainEventEnvelopeSerializer serializer, IClock clock)
{
    /// <summary>
    /// Wraps everything raised during this request.
    /// </summary>
    /// <param name="entries">The tracked aggregates.</param>
    /// <returns>
    /// One envelope per uncommitted event, across all aggregates, each numbered with the version it
    /// produced and all of them carrying the same timestamp.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A domain event has no persisted name, because its assembly was not registered with
    /// <c>AddDomainEventsFrom</c>.
    /// </exception>
    /// <remarks>
    /// Versions are counted back from the aggregate's current version, so the events of one request
    /// are numbered consecutively and end at the version the aggregate now holds.
    /// </remarks>
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
