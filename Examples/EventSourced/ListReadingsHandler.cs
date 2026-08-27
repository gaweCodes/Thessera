using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;

namespace EventSourced;

public sealed class ListReadingsHandler(IReadingStreamCatalog streamCatalog, IRepository<Reading, ReadingId> repository)
    : IQueryHandler<ListReadings, ReadingListResponse>
{
    public async Task<Result<ReadingListResponse>> HandleAsync(ListReadings query, CancellationToken cancellationToken)
    {
        var streamKeys = await streamCatalog.ListStreamKeysAsync(cancellationToken).ConfigureAwait(false);

        var parsedIds = new List<int>();
        foreach (var streamKey in streamKeys)
        {
            var rawId = streamKey[EventSourcedApplication.StreamKeyPrefix.Length..];
            if (int.TryParse(rawId, out var value))
            {
                parsedIds.Add(value);
            }
        }

        var snapshots = new List<ReadingSnapshot>();
        foreach (var value in parsedIds.Order())
        {
            var reading = await repository.GetByIdAsync(new ReadingId(value), cancellationToken).ConfigureAwait(false);
            if (reading is not null && !reading.IsDeleted)
            {
                snapshots.Add(ReadingSnapshot.From(reading));
            }
        }

        return new ReadingListResponse("List", snapshots, []);
    }
}
