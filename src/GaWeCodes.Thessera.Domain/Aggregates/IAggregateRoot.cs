using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Domain.Aggregates;

/// <summary>
/// The unit that is loaded, changed and saved as a whole, and the only thing a repository hands
/// out.
/// </summary>
/// <typeparam name="TKey">
/// The aggregate's typed identity. A value type implementing <see cref="IEntityKey"/>, so that two
/// identities of different kinds cannot be passed to one another.
/// </typeparam>
/// <remarks>
/// This is the contract the rest of the family binds against; derive from
/// <see cref="AggregateRoot{TKey, TState}"/> or <see cref="EventSourcedAggregateRoot{TKey, TState}"/>
/// to get an implementation. Which of the two you pick decides which stores the aggregate can run
/// on — see <see cref="IEventSourcedAggregateRoot{TKey}"/>.
/// </remarks>
public interface IAggregateRoot<TKey> : IEntity<TKey>, IHasDomainEvents
    where TKey : struct, IEntityKey, IEquatable<TKey>;
