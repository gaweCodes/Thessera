using GaWeCodes.Thessera.Application.Persistence;

namespace DomainApplication;

public sealed class InMemoryReadingRepository : IRepository<Reading, ReadingId>, IReadingCatalog
{
    private readonly Dictionary<ReadingId, Reading> _readings = [];

    public Task<Reading?> GetByIdAsync(ReadingId id, CancellationToken cancellationToken)
    {
        _readings.TryGetValue(id, out var reading);
        return Task.FromResult(reading);
    }

    public Task AddAsync(Reading aggregate, CancellationToken cancellationToken)
    {
        _readings[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReadingSnapshot>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReadingSnapshot>>(
            [.. _readings.Values
                .Where(reading => !reading.IsDeleted)
                .OrderBy(reading => reading.CreatedAt)
                .Select(ReadingSnapshot.From)]);
}
