using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;

namespace ContractsHost;

public static class MatrixHost
{
    public static async Task<Result<int>> ProbeAsync(CancellationToken cancellationToken)
    {
        var readings = new InMemoryReadings();

        var recorded = await new RecordReadingHandler(readings)
            .HandleAsync(new RecordReading(72), cancellationToken)
            .ConfigureAwait(false);

        return await new ReadingByIdHandler(readings)
            .HandleAsync(new ReadingById(recorded.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<Result<int>> ProbeMissingAsync(CancellationToken cancellationToken) =>
        await new ReadingByIdHandler(new InMemoryReadings())
            .HandleAsync(new ReadingById(ReadingId.New()), cancellationToken)
            .ConfigureAwait(false);

    public static async Task<IReadOnlyList<ReadingSummary>> ProbeProjectionAsync(
        CancellationToken cancellationToken)
    {
        var store = new InMemorySummaries();
        var projection = new ReadingSummaryProjection(store);
        var reading = Reading.Record(ReadingId.New(), 72);
        reading.Correct(75);

        var aggregateId = reading.Id.Value.ToString();
        var version = 0L;

        foreach (var domainEvent in reading.DomainEvents)
        {
            var metadata = new DomainEventMetadata(
                Guid.NewGuid(),
                "matrix-reading",
                aggregateId,
                ++version,
                DateTimeOffset.UnixEpoch);

            switch (domainEvent)
            {
                case ReadingRecorded recorded:
                    await projection.HandleAsync(recorded, metadata, cancellationToken).ConfigureAwait(false);
                    break;
                case ReadingCorrected corrected:
                    await projection.HandleAsync(corrected, metadata, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"The projection probe does not cover the event '{domainEvent.GetType()}'.");
            }
        }

        return store.Written;
    }

    private sealed class InMemorySummaries : IReadingSummaryStore
    {
        private readonly List<ReadingSummary> _written = [];

        public IReadOnlyList<ReadingSummary> Written => _written;

        public Task WriteAsync(ReadingSummary summary, CancellationToken cancellationToken)
        {
            _written.Add(summary);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryReadings : IRepository<Reading, ReadingId>
    {
        private readonly Dictionary<ReadingId, Reading> _readings = [];

        public Task<Reading?> GetByIdAsync(ReadingId id, CancellationToken cancellationToken) =>
            Task.FromResult(_readings.GetValueOrDefault(id));

        public Task AddAsync(Reading aggregate, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(aggregate);

            _readings[aggregate.Id] = aggregate;
            return Task.CompletedTask;
        }
    }
}
