using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

/// <summary>
/// An aggregate root that can additionally be rebuilt from its stored events.
/// </summary>
/// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
/// <typeparam name="TState">The aggregate's state record.</typeparam>
/// <param name="initialState">
/// The empty starting state, from which the first event — or the whole replayed history — is
/// applied.
/// </param>
/// <remarks>
/// This adds replay and nothing else: the same state record, the same <c>Apply</c>, the same rules
/// as <see cref="AggregateRoot{TKey, TState}"/>. Choosing it decides how portable the model is —
/// an aggregate derived from this class runs on both store choices, while a plain
/// <see cref="AggregateRoot{TKey, TState}"/> runs on a state store only.
/// </remarks>
public abstract class EventSourcedAggregateRoot<TKey, TState>(TState initialState)
    : AggregateRoot<TKey, TState>(initialState), IEventSourcedAggregateRoot<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
    where TState : AggregateState<TState, TKey>
{
    private bool _loaded;

    void IEventSourcedAggregateRoot<TKey>.LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (DomainEvents.Count > 0)
        {
            throw new InvalidOperationException(
                "LoadFromHistory cannot be called after events have been raised on the aggregate.");
        }

        if (_loaded)
        {
            throw new InvalidOperationException(
                "LoadFromHistory cannot be called twice on the same aggregate. The second call replays the "
                + "history onto the state the first one already produced, which counts every event a second "
                + "time: the version advances twice and anything the events accumulate is duplicated. Load a "
                + "fresh instance instead.");
        }

        _loaded = true;

        foreach (var domainEvent in history)
        {
            ApplyEvent(domainEvent);
        }
    }
}
