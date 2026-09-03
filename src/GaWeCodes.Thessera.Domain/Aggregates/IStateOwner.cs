namespace GaWeCodes.Thessera.Domain.Aggregates;

/// <summary>
/// The untyped view of an aggregate's state that a store adapter writes against, so that a
/// repository can persist any aggregate without knowing its state type at compile time.
/// </summary>
/// <remarks>
/// An aggregate's state is an immutable record that is <em>replaced</em> on every applied event. A
/// tracker that keeps the object it loaded, rather than reading <see cref="State"/> again at save
/// time, will store the old state and report success. Track the aggregate; read the state when you
/// commit.
/// </remarks>
public interface IStateOwner
{
    /// <summary>
    /// Gets the CLR type of the state record, so that a store can look up its mapping.
    /// </summary>
    Type StateType { get; }

    /// <summary>
    /// Gets the aggregate's current state — the object as it is <em>now</em>, after every event
    /// applied so far.
    /// </summary>
    object State { get; }

    /// <summary>
    /// Gets the version the state has reached: the number of events applied to it.
    /// </summary>
    /// <value>
    /// Zero for an aggregate that has applied nothing. A state store maps this as the
    /// optimistic-concurrency token; an event store uses it as the expected stream version.
    /// </value>
    long Version { get; }

    /// <summary>
    /// Puts a loaded state back into an empty aggregate hull.
    /// </summary>
    /// <param name="state">
    /// The state read from the store. Must be of <see cref="StateType"/> and must carry a non-empty
    /// identity.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The aggregate already has uncommitted domain events. Restoring at that point would replace
    /// the state those events were raised against while leaving them recorded, so the aggregate's
    /// state and its events would no longer agree; restore into a fresh hull instead.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="state"/> is not of <see cref="StateType"/>.
    /// </exception>
    /// <exception cref="Rules.DomainValidationException">
    /// <paramref name="state"/> carries an empty identity, which would produce an aggregate that
    /// cannot be addressed.
    /// </exception>
    void Restore(object state);
}
