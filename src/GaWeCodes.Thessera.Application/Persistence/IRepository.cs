using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Application.Persistence;

/// <summary>
/// Loads and adds aggregates. The only way an application layer reaches one.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type this repository serves.</typeparam>
/// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
/// <remarks>
/// Deliberately narrow: two methods, and no query surface. Everything else an aggregate needs is a
/// method on the aggregate, and anything that reads across many of them is a query against a read
/// model rather than a repository call.
/// <para>
/// There is no <c>Save</c>. The unit of work commits once per command; whether that same
/// transaction also writes the outbox is runtime-dependent (see the package README).
/// </para>
/// </remarks>
public interface IRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    /// <summary>
    /// Loads one aggregate by its identity.
    /// </summary>
    /// <param name="id">The aggregate's identity.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The aggregate, tracked so that its changes are picked up at commit, or
    /// <see langword="null"/> when no aggregate has that identity.
    /// </returns>
    /// <remarks>
    /// On an event store this replays the aggregate's stream; on a state store it reads the stored
    /// state. The aggregate that comes back is the same either way.
    /// </remarks>
    Task<TAggregate?> GetByIdAsync(TKey id, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a newly created aggregate under the unit of work's care.
    /// </summary>
    /// <param name="aggregate">The aggregate to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the aggregate is tracked.</returns>
    /// <remarks>
    /// Nothing is written here. The aggregate and the events it has raised are persisted when the
    /// unit of work commits at the end of the command.
    /// </remarks>
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken);
}
