using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Persistence;

/// <summary>
/// Remembers which aggregates the current unit of work is responsible for, so that at commit time
/// their events can be handed to the envelope factory.
/// </summary>
/// <typeparam name="TEntry">
/// The store's own entry type, which may carry more than the shared contract — a state store also
/// needs the entity it loaded, for instance.
/// </typeparam>
/// <remarks>
/// Registered per scope and filled by the repository. Adding the same aggregate twice is a no-op, so
/// loading it and then adding it again cannot produce its events twice.
/// </remarks>
public abstract class AggregateTracker<TEntry>
    where TEntry : class, ITrackedAggregate
{
    private readonly List<TEntry> _entries = [];

    /// <summary>
    /// Gets the aggregates being tracked, in the order they were added.
    /// </summary>
    public IReadOnlyList<TEntry> Entries => _entries;

    /// <summary>
    /// Drops the uncommitted events of every tracked aggregate and forgets them all.
    /// </summary>
    /// <remarks>
    /// Called by the unit of work <em>after</em> a successful commit, and by nothing else. Calling
    /// it earlier risks losing events nobody has durably published yet — whether that publishing
    /// is an outbox at all is runtime-dependent; see "What this package promises" in the package
    /// README.
    /// </remarks>
    public void ClearDomainEvents()
    {
        foreach (var entry in _entries)
        {
            entry.Aggregate.ClearDomainEvents();
        }

        _entries.Clear();
    }

    /// <summary>
    /// Starts tracking one aggregate.
    /// </summary>
    /// <param name="entry">The aggregate and what the store needs to know about it.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entry"/> or its aggregate is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The entry carries no aggregate name or no identity — either would produce a stream key that
    /// addresses nothing.
    /// </exception>
    /// <remarks>
    /// Adding the same aggregate instance again does nothing, so a repository may call this on every
    /// load without checking first.
    /// </remarks>
    protected void Add(TEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Aggregate);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.AggregateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.AggregateId);

        if (_entries.Exists(existing => ReferenceEquals(existing.Aggregate, entry.Aggregate)))
        {
            return;
        }

        _entries.Add(entry);
    }
}
