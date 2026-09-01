using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Rules;

namespace GaWeCodes.Thessera.Domain.Aggregates;

/// <summary>
/// The state-stored form of an aggregate root: it holds its state, raises domain events, and
/// collects them until the unit of work has committed.
/// </summary>
/// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
/// <typeparam name="TState">The aggregate's state record.</typeparam>
/// <remarks>
/// A deriving type carries an <see cref="Naming.AggregateNameAttribute"/> and keeps its
/// parameterless constructor <see langword="private"/>: a repository reconstitutes an empty hull
/// through it, while callers go through a named factory method. Public creation would let an
/// aggregate come into existence without the rules its factory checks.
/// <para>
/// Derive from <see cref="EventSourcedAggregateRoot{TKey, TState}"/> instead when the aggregate
/// should also be replayable from its events. That choice decides which stores it can run on.
/// </para>
/// </remarks>
public abstract class AggregateRoot<TKey, TState> : EntityBase<TKey>, IAggregateRoot<TKey>, IDomainEventOwner, IDomainEventRaiser, IStateOwner
    where TKey : struct, IEntityKey, IEquatable<TKey>
    where TState : AggregateState<TState, TKey>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateRoot{TKey, TState}"/> class with the
    /// state it starts from.
    /// </summary>
    /// <param name="initialState">
    /// The empty starting state. Its identity is normally still empty at this point and is set by
    /// the first applied event.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="initialState"/> is <see langword="null"/>.</exception>
    protected AggregateRoot(TState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        State = initialState;
    }

    /// <summary>
    /// Gets the aggregate's current state — replaced, not mutated, on every applied event.
    /// </summary>
    /// <remarks>
    /// Expose what callers need through properties and methods of the aggregate rather than
    /// handing the state out; the state is data, and the aggregate is where the rules live.
    /// </remarks>
    protected TState State { get; private set; }

    /// <inheritdoc/>
    public sealed override TKey Id => State.Id;

    /// <summary>
    /// Gets the events raised since the last commit, oldest first.
    /// </summary>
    /// <value>
    /// The uncommitted events, read by the unit of work at commit time. Wrapping each into an
    /// envelope, writing it to the outbox and clearing the list afterwards is runtime-dependent;
    /// see "What this package promises" in the package README.
    /// </value>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Applies an event to the aggregate's state and records it as uncommitted.
    /// </summary>
    /// <param name="domainEvent">The event that has happened.</param>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <c>Apply</c> returned <see langword="null"/> for the event. An unhandled event returns the
    /// state unchanged; <see langword="null"/> is never a state an aggregate can be in.
    /// </exception>
    /// <exception cref="DomainValidationException">
    /// The aggregate's identity is still empty after the event was applied, so the aggregate could
    /// not be addressed or stored.
    /// </exception>
    /// <remarks>
    /// Check the rules <em>before</em> raising. An event is a fact that has already happened, and
    /// <c>Apply</c> is not the place to refuse one — an aggregate that refused its own history
    /// could not be replayed.
    /// </remarks>
    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ApplyEvent(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    private protected void ApplyEvent(IDomainEvent domainEvent)
    {
        var applied = State.Apply(domainEvent)
            ?? throw new InvalidOperationException(
                $"'{typeof(TState)}.Apply' returned null for the event '{domainEvent.GetType()}'. Applying an "
                + "event returns the state that follows it, and an unhandled event returns the state unchanged; "
                + "null is never a state an aggregate can be in.");

        State = applied.WithVersion(State.Version + 1);

        if (State.Id.IsEmpty)
        {
            throw new DomainValidationException(
                "The aggregate's identity must be set to a non-empty value by the applied event.");
        }
    }

    void IDomainEventOwner.ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    void IDomainEventRaiser.Raise(IDomainEvent domainEvent)
    {
        RaiseEvent(domainEvent);
    }

    Type IStateOwner.StateType => typeof(TState);

    object IStateOwner.State => State;

    long IStateOwner.Version => State.Version;

    void IStateOwner.Restore(object state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state is not TState typedState)
        {
            throw new ArgumentException(
                $"The state must be of type '{typeof(TState)}', but was '{state.GetType()}'.",
                nameof(state));
        }

        if (typedState.Id.IsEmpty)
        {
            throw new DomainValidationException(
                "The restored state must carry a non-empty identity.");
        }

        State = typedState;
    }
}
