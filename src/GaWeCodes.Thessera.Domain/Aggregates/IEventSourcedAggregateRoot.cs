using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

/// <summary>
/// An aggregate that can be rebuilt by replaying the events that produced it.
/// </summary>
/// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
/// <remarks>
/// Implementing this interface is what makes an aggregate event-sourced as far as the runtime is
/// concerned: the aggregate style is read from the type rather than configured. Catching a
/// mismatch against the selected store at startup, rather than the first time it is loaded or
/// saved, is runtime-dependent; see "What this package promises" in the package README.
/// <para>
/// An event-sourced aggregate runs on both store choices — on an event store it keeps its stream,
/// and on a state store it keeps state and version once that host has said <c>WithoutEventHistory()</c>.
/// The reverse does not hold: a plain aggregate cannot run on an event store, because there is no
/// history to replay it from.
/// </para>
/// </remarks>
public interface IEventSourcedAggregateRoot<TKey> : IAggregateRoot<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    /// <summary>
    /// Rebuilds the aggregate by applying every event of its stream, oldest first.
    /// </summary>
    /// <param name="history">
    /// The stored events, in the order they were appended. Applying them out of order produces a
    /// state that never existed.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="history"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Called after an event has already been raised on this aggregate, or called a second time on
    /// the same instance — either would replay history onto state that is no longer empty and
    /// count events twice.
    /// </exception>
    /// <remarks>
    /// Called by an event-sourced repository when loading, on a freshly reconstituted, otherwise
    /// untouched instance. The replayed events are not recorded as uncommitted, so a load followed
    /// by a commit writes nothing.
    /// </remarks>
    void LoadFromHistory(IEnumerable<IDomainEvent> history);
}
