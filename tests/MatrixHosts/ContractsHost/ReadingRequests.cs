using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;

namespace ContractsHost;

public sealed record RecordReading(int Value) : ICommand<ReadingId>;

public sealed class RecordReadingHandler(IRepository<Reading, ReadingId> readings)
    : ICommandHandler<RecordReading, ReadingId>
{
    public async Task<Result<ReadingId>> HandleAsync(RecordReading command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reading = Reading.Record(ReadingId.New(), command.Value);
        await readings.AddAsync(reading, cancellationToken).ConfigureAwait(false);

        return reading.Id;
    }
}

public sealed record ReadingById(ReadingId ReadingId) : IQuery<int>;

public sealed class ReadingByIdHandler(IRepository<Reading, ReadingId> readings)
    : IQueryHandler<ReadingById, int>
{
    public async Task<Result<int>> HandleAsync(ReadingById query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var reading = await readings.GetByIdAsync(query.ReadingId, cancellationToken).ConfigureAwait(false);

        return reading is null
            ? Failure.NotFound("reading.not-found", "No reading exists for the requested identity.")
            : reading.Value;
    }
}
