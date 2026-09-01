namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Whether an aggregate is stored as its current state or as the stream of events that produced it.
/// </summary>
/// <remarks>
/// An aggregate's style is not configured — it is read from its base class, by whether the type
/// implements <c>IEventSourcedAggregateRoot</c>. A store declares the style it supports, and a
/// startup check compares the two, because a mismatch is otherwise either an unbuildable repository
/// or a silently discarded history.
/// </remarks>
public enum AggregateStyle
{
    /// <summary>
    /// Only the current state is kept, overwritten on every change. The past is not retained.
    /// </summary>
    StateStored,

    /// <summary>
    /// The events are kept and the current state is derived by replaying them, so the aggregate can
    /// be audited and inspected as of an earlier point in time.
    /// </summary>
    EventSourced,
}
