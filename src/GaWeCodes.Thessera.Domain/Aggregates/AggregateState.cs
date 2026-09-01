using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

/// <summary>
/// The immutable record that holds an aggregate's data and folds events into new versions of
/// itself.
/// </summary>
/// <typeparam name="TSelf">
/// The deriving record itself. Naming any other type compiles and then fails as an
/// <see cref="InvalidCastException"/> the first time an event is applied. Catching this earlier,
/// at startup, is runtime-dependent; see "What this package promises" in the package README.
/// </typeparam>
/// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
/// <remarks>
/// The state carries the data; the aggregate root carries the behaviour and the rules. Keeping them
/// apart is what lets the same model be stored either as state or as the events that produced it.
/// </remarks>
public abstract record AggregateState<TSelf, TKey>
    where TSelf : AggregateState<TSelf, TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    /// <summary>
    /// Gets the aggregate's identity.
    /// </summary>
    /// <value>
    /// Set by the event that creates the aggregate. It must be non-empty afterwards: an aggregate
    /// without an identity cannot be addressed, and the applied event is checked for it.
    /// </value>
    public abstract TKey Id { get; init; }

    /// <summary>
    /// Gets the number of events applied to this state.
    /// </summary>
    /// <value>
    /// Zero on a fresh state, incremented by one per applied event. Map it as the concurrency token
    /// on a state store; an event store uses it as the expected stream version.
    /// </value>
    public long Version { get; init; }

    /// <summary>
    /// Returns the state that follows <paramref name="domainEvent"/>.
    /// </summary>
    /// <param name="domainEvent">The event to fold in.</param>
    /// <returns>
    /// A new state for an event this type knows, and <see langword="this"/> unchanged for one it
    /// does not. Returning <see langword="null"/> is rejected — <see langword="null"/> is never a
    /// state an aggregate can be in.
    /// </returns>
    /// <remarks>
    /// Implementations hold no side effects and enforce no rules: by the time an event is applied
    /// it has already happened, and refusing it here would leave the aggregate unable to replay its
    /// own history. Rules belong in the method that raises the event.
    /// </remarks>
    public abstract TSelf Apply(IDomainEvent domainEvent);

    internal TSelf WithVersion(long version) => (TSelf)(object)(this with { Version = version });
}
