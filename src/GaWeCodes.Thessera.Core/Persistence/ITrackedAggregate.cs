using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// One aggregate a store is holding for the current unit of work, in the shape the envelope factory
/// needs to build the envelopes for its events.
/// </summary>
/// <remarks>
/// Track the aggregate, not its state. An aggregate's state is an immutable record that is replaced
/// on every applied event, so an entry that captured the object it loaded would write the old state
/// and report success.
/// </remarks>
public interface ITrackedAggregate
{
    /// <summary>
    /// Gets the aggregate itself, as the thing that owns the uncommitted events.
    /// </summary>
    IDomainEventOwner Aggregate { get; }

    /// <summary>
    /// Gets the aggregate's persisted name, from its <see cref="AggregateNameAttribute"/>.
    /// </summary>
    string AggregateName { get; }

    /// <summary>
    /// Gets the aggregate's identity, rendered in the pinned stream-key format.
    /// </summary>
    string AggregateId { get; }

    /// <summary>
    /// Gets the version the aggregate was at when the unit of work began tracking it.
    /// </summary>
    /// <value>
    /// The version <em>before</em> this request's events. An event store appends at this version as
    /// its optimistic-concurrency expectation, and the envelopes number the new events up from it.
    /// </value>
    long CurrentVersion { get; }
}
