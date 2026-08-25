using GaWeCodes.Thessera.Core.Persistence;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

internal sealed record PersistenceChoice
{
    private PersistenceChoice(IPersistenceAdapter? adapter, string description, bool isChosen)
    {
        Adapter = adapter;
        Description = description;
        IsChosen = isChosen;
    }

    public static PersistenceChoice None { get; } = new(null, "none", isChosen: false);

    public static PersistenceChoice NoPersistence { get; } = new(null, "UseNoPersistence", isChosen: true);

    public IPersistenceAdapter? Adapter { get; }

    public string Description { get; }

    public bool IsChosen { get; }

    public bool IsSelected => Adapter is not null;

    public bool IsDeliberatelyWithoutPersistence => IsChosen && Adapter is null;

    public string? WriteConnectionString => Adapter?.WriteConnectionString;

    public bool IsTransientFault(Exception exception) => Adapter?.IsTransientFault(exception) ?? false;

    public static PersistenceChoice For(IPersistenceAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        return new PersistenceChoice(adapter, adapter.Description, isChosen: true);
    }
}
