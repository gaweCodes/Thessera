namespace DomainApplication;

public interface IReadingCatalog
{
    Task<IReadOnlyList<ReadingSnapshot>> ListAsync(CancellationToken cancellationToken);
}
