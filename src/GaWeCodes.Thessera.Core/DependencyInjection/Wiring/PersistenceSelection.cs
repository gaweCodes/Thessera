namespace GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

internal sealed class PersistenceSelection
{
    private readonly List<PersistenceChoice> _choices = [];

    public bool IsChosen => _choices.Count > 0;

    public bool IsEventHistoryWaived { get; private set; }

    public bool IsSelected => _choices.Exists(static choice => choice.IsSelected);

    public bool IsDeliberatelyWithoutPersistence =>
        _choices is [{ IsDeliberatelyWithoutPersistence: true }];

    /// <summary>
    /// Every store selected on this host, in the order they were selected.
    /// </summary>
    public IReadOnlyList<PersistenceChoice> Choices => _choices;

    public bool IsTransientFault(Exception exception) => _choices.Exists(choice => choice.IsTransientFault(exception));

    public void WaiveEventHistory() => IsEventHistoryWaived = true;

    /// <summary>
    /// Finds the store that owns <paramref name="aggregateType"/>: the store that explicitly claims
    /// it, or otherwise the one main store (the one selected without a claim list).
    /// </summary>
    public PersistenceChoice? ResolveChoice(Type aggregateType) =>
        _choices.Find(choice => choice.IsSelected && choice.ClaimedAggregates.Contains(aggregateType))
        ?? _choices.Find(static choice => choice.IsSelected && choice.ClaimedAggregates.Count == 0);

    public void Select(PersistenceChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        if (choice.IsDeliberatelyWithoutPersistence)
        {
            SelectNoPersistence(choice);
            return;
        }

        if (_choices.Exists(static existing => existing.IsDeliberatelyWithoutPersistence))
        {
            throw NoPersistenceCombinedWith(choice);
        }

        if (_choices.Exists(existing => existing.IsSameConfigurationAs(choice)))
        {
            return;
        }

        var sameAdapterType = _choices.Find(existing => existing.Adapter?.GetType() == choice.Adapter?.GetType());
        if (sameAdapterType is not null)
        {
            throw new InvalidOperationException(
                $"{choice.Description} was called twice with different arguments. A bounded context has exactly " +
                "one write database per store, so the second call would silently point some aggregates and the " +
                "outbox at different databases.");
        }

        ThrowIfAggregatesAreClaimedTwice(choice);
        ThrowIfASecondMainStore(choice);

        _choices.Add(choice);
    }

    private void SelectNoPersistence(PersistenceChoice choice)
    {
        if (_choices.Count == 0)
        {
            _choices.Add(choice);
            return;
        }

        if (_choices is [{ IsDeliberatelyWithoutPersistence: true }])
        {
            return;
        }

        throw NoPersistenceCombinedWith(_choices[0]);
    }

    private void ThrowIfAggregatesAreClaimedTwice(PersistenceChoice choice)
    {
        foreach (var aggregate in choice.ClaimedAggregates)
        {
            var owner = _choices.Find(existing => existing.ClaimedAggregates.Contains(aggregate));
            if (owner is null)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"'{aggregate}' is claimed by both {owner.Description} and {choice.Description}. An aggregate is " +
                "owned by exactly one store, because a commit cannot span two databases. Remove it from one of " +
                "the two 'forAggregates' lists.");
        }
    }

    private void ThrowIfASecondMainStore(PersistenceChoice choice)
    {
        if (choice.ClaimedAggregates.Count > 0)
        {
            return;
        }

        var existingMain = _choices.Find(static existing => existing.IsSelected && existing.ClaimedAggregates.Count == 0);
        if (existingMain is null)
        {
            return;
        }

        throw new InvalidOperationException(
            "Two persistence strategies were configured for the same host " +
            $"({existingMain.Description} and {choice.Description}). " +
            "A store selected without a 'forAggregates' list is the host's main store and owns every aggregate no " +
            "other store claims, so at most one store may be selected this way. Give the second store an " +
            "explicit 'forAggregates' list of the aggregates it owns, or split the context in two if it should " +
            "not share aggregates with the first store at all.");
    }

    private static InvalidOperationException NoPersistenceCombinedWith(PersistenceChoice storeChoice) =>
        new($"UseNoPersistence was combined with {storeChoice.Description}. " +
            "UseNoPersistence states that this host deliberately commits nothing, so it cannot be combined " +
            "with a persistence strategy. Keep exactly one of the two.");
}
