using GaWeCodes.Thessera.Core.Persistence;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

/// <summary>
/// One store selected for this host, together with the aggregates it owns.
/// </summary>
/// <remarks>
/// An empty <see cref="ClaimedAggregates"/> makes this the host's "main" store: it owns every
/// aggregate no other selected store claims. At most one main store may exist; every other store is
/// "ancillary" and must name exactly the aggregates it owns through the <c>forAggregates</c>
/// parameter on its <c>Use*Store</c> entry point.
/// <para>
/// Deliberately a plain class rather than a record: <see cref="PersistenceSelection"/> never compares
/// two choices through <c>==</c> (arrays and delegates captured by an adapter do not compare
/// structurally in a useful way), so this type carries no equality semantics of its own.
/// </para>
/// </remarks>
internal sealed class PersistenceChoice
{
    private PersistenceChoice(
        IPersistenceAdapter? adapter,
        string description,
        bool isChosen,
        IReadOnlyCollection<Type> claimedAggregates)
    {
        Adapter = adapter;
        Description = description;
        IsChosen = isChosen;
        ClaimedAggregates = claimedAggregates;
    }

    public static PersistenceChoice None { get; } = new(null, "none", isChosen: false, []);

    public static PersistenceChoice NoPersistence { get; } = new(null, "UseNoPersistence", isChosen: true, []);

    public IPersistenceAdapter? Adapter { get; }

    public string Description { get; }

    public bool IsChosen { get; }

    /// <summary>
    /// Gets the aggregates this store owns, or an empty collection when it is the host's main store
    /// and therefore owns every aggregate no other selected store claims.
    /// </summary>
    public IReadOnlyCollection<Type> ClaimedAggregates { get; }

    /// <summary>
    /// Gets a stable identity for this store, used to key its unit of work and repositories apart
    /// from every other store selected on the same host.
    /// </summary>
    public string StoreId { get; } = Guid.NewGuid().ToString("N");

    public bool IsSelected => Adapter is not null;

    public bool IsDeliberatelyWithoutPersistence => IsChosen && Adapter is null;

    public string? WriteConnectionString => Adapter?.WriteConnectionString;

    public bool IsTransientFault(Exception exception) => Adapter?.IsTransientFault(exception) ?? false;

    public static PersistenceChoice For(IPersistenceAdapter adapter, IReadOnlyCollection<Type>? claimedAggregates = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        return new PersistenceChoice(adapter, adapter.Description, isChosen: true, claimedAggregates ?? []);
    }

    /// <summary>
    /// Whether this choice describes the exact same store selection as <paramref name="other"/> —
    /// same adapter, by its own equality, and the same set of claimed aggregates.
    /// </summary>
    public bool IsSameConfigurationAs(PersistenceChoice other) =>
        Equals(Adapter, other.Adapter) && ClaimedAggregates.ToHashSet().SetEquals(other.ClaimedAggregates);
}
