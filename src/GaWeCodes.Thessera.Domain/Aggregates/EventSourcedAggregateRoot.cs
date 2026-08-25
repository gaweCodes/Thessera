using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

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
