namespace GaWeCodes.Thessera.Core.Dispatching;

internal sealed class CommandStoreRouter
{
    private readonly Dictionary<Type, string> _storeIdByCommand = [];

    public void Route(Type commandType, string storeId) => _storeIdByCommand[commandType] = storeId;

    public string? StoreIdFor(Type commandType) => _storeIdByCommand.GetValueOrDefault(commandType);
}
