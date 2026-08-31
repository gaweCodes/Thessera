using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Application.ReadModels;

/// <summary>
/// Rebuilds a read model from scratch: clear it, then feed every aggregate through it again.
/// </summary>
/// <typeparam name="TAggregate">The aggregate the read model is derived from.</typeparam>
/// <typeparam name="TKey">The aggregate's typed identity.</typeparam>
/// <remarks>
/// You implement this; the store package supplies the runner that drives it in batches. A rebuild
/// is how a changed projection catches up — which is possible only because a read model is derived
/// and never the record of truth.
/// <para>
/// On an event store the runner replays each stream, so the rebuilt model reflects what actually
/// happened. On a state store there is no history, so it is reconstructed from current state only.
/// </para>
/// </remarks>
public interface IReadModelRebuilder<in TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : struct, IEntityKey, IEquatable<TKey>
{
    /// <summary>
    /// Empties the read model before it is rebuilt.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the read model is empty.</returns>
    /// <remarks>
    /// Called once, before the first batch. The read model is unusable between this call and the end
    /// of the rebuild, so run a rebuild where that is acceptable.
    /// </remarks>
    Task ClearAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes one aggregate's contribution to the read model.
    /// </summary>
    /// <param name="aggregate">The rebuilt aggregate, in its current state.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once this aggregate has been written.</returns>
    Task RebuildAsync(TAggregate aggregate, CancellationToken cancellationToken);
}
