using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Persistence;

public abstract class AggregateTracker<TEntry>
    where TEntry : class, ITrackedAggregate
{
    private readonly List<TEntry> _entries = [];

    public IReadOnlyList<TEntry> Entries => _entries;

    public void ClearDomainEvents()
    {
        foreach (var entry in _entries)
        {
            entry.Aggregate.ClearDomainEvents();
        }

        _entries.Clear();
    }

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
