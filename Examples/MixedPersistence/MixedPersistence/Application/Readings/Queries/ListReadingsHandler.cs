using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;

namespace MixedPersistence;

public sealed class ListReadingsHandler(IReadingReadModelStore readModel) : IQueryHandler<ListReadings, ReadingListResponse>
{
    public Task<Result<ReadingListResponse>> HandleAsync(ListReadings query, CancellationToken cancellationToken)
    {
        var snapshots = readModel.All()
            .Where(snapshot => !snapshot.IsDeleted)
            .OrderBy(snapshot => snapshot.Id)
            .ToList();

        Result<ReadingListResponse> result = new ReadingListResponse("List", snapshots, []);
        return Task.FromResult(result);
    }
}
