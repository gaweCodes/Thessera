namespace GaWeCodes.Thessera.Core.Dispatching;

/// <summary>
/// Maps a command to the store it commits through, when a host has more than one.
/// </summary>
/// <remarks>
/// Empty on a host with at most one store — the common case — so <see cref="UnitOfWorkBehavior{TRequest,TResponse}"/>
/// resolves the one unkeyed <c>IUnitOfWork</c> exactly as it always has. Populated once, before any
/// request is dispatched, by a startup check that inspects every registered command handler's
/// constructor for the aggregates its repositories touch.
/// </remarks>
internal sealed class CommandStoreRouter
{
    private readonly Dictionary<Type, string> _storeIdByCommand = [];

    /// <summary>
    /// Records that <paramref name="commandType"/> commits through the store identified by
    /// <paramref name="storeId"/>.
    /// </summary>
    public void Route(Type commandType, string storeId) => _storeIdByCommand[commandType] = storeId;

    /// <summary>
    /// The store id <paramref name="commandType"/> was routed to, or <see langword="null"/> when it
    /// commits through the host's one main, unkeyed unit of work.
    /// </summary>
    public string? StoreIdFor(Type commandType) => _storeIdByCommand.GetValueOrDefault(commandType);
}
