namespace MixedPersistenceWithMessaging;

public interface IReadingStreamCatalog
{
    Task<int> GetMaxIdAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListStreamKeysAsync(CancellationToken cancellationToken);
}
