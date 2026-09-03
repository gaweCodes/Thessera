using GaWeCodes.Thessera.Application.ReadModels;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.ReadModels;

/// <summary>
/// The half of a read-model rebuild that is the same whatever the store: find the rebuilders, clear
/// them, and feed them aggregates a batch at a time.
/// </summary>
/// <param name="scopeFactory">
/// Opens a scope per call, so a long rebuild does not hold one open across the whole run.
/// </param>
/// <remarks>
/// A store package supplies the other half — reading state, or replaying streams — and drives this.
/// Consumers use the runner their store registered rather than this type directly.
/// </remarks>
public sealed class ReadModelRebuildWriter(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// How many aggregates a store should hand over per call.
    /// </summary>
    /// <remarks>
    /// Large enough that a rebuild is not dominated by scope creation, small enough that a batch and
    /// its scope stay bounded in memory.
    /// </remarks>
    public const int BatchSize = 500;

    /// <summary>
    /// Empties the read models derived from one aggregate type.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate the read models are derived from.</typeparam>
    /// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once every registered rebuilder has cleared.</returns>
    /// <exception cref="InvalidOperationException">
    /// No <c>IReadModelRebuilder</c> is registered for <typeparamref name="TAggregate"/>. A rebuild
    /// that projects nothing would otherwise report success while the read model stays empty.
    /// </exception>
    /// <remarks>
    /// Every rebuilder registered for the aggregate is cleared, so an aggregate feeding several read
    /// models rebuilds all of them together.
    /// </remarks>
    public async Task ClearAsync<TAggregate, TKey>(CancellationToken cancellationToken)
        where TAggregate : class, IAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        using var scope = scopeFactory.CreateScope();

        foreach (var rebuilder in RebuildersOf<TAggregate, TKey>(scope.ServiceProvider))
        {
            await rebuilder.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Feeds one batch of rebuilt aggregates through every registered rebuilder.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate the read models are derived from.</typeparam>
    /// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
    /// <param name="batch">
    /// The aggregates to write, at most <see cref="BatchSize"/> of them.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the batch has been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="batch"/> is <see langword="null"/>.</exception>
    public async Task WriteAsync<TAggregate, TKey>(
        IReadOnlyList<TAggregate> batch,
        CancellationToken cancellationToken)
        where TAggregate : class, IAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(batch);

        using var scope = scopeFactory.CreateScope();
        var rebuilders = RebuildersOf<TAggregate, TKey>(scope.ServiceProvider);

        foreach (var aggregate in batch)
        {
            foreach (var rebuilder in rebuilders)
            {
                await rebuilder.RebuildAsync(aggregate, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static IReadModelRebuilder<TAggregate, TKey>[] RebuildersOf<TAggregate, TKey>(
        IServiceProvider services)
        where TAggregate : class, IAggregateRoot<TKey>
        where TKey : struct, IEntityKey, IEquatable<TKey>
    {
        var rebuilders = services.GetServices<IReadModelRebuilder<TAggregate, TKey>>().ToArray();

        return rebuilders.Length > 0
            ? rebuilders
            : throw new InvalidOperationException(
                $"No {typeof(IReadModelRebuilder<,>).Name} was registered for aggregate '{typeof(TAggregate)}'. " +
                "A rebuild that projects nothing reports success while the read model stays empty; " +
                "register one through AddHandlersFrom, or do not run the rebuild.");
    }
}
