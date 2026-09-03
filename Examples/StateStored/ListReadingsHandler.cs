using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;

namespace StateStored;

public sealed class ListReadingsHandler(IReadingReadModelStore readModel) : IQueryHandler<ListReadings, ReadingListResponse>
{
    public Task<Result<ReadingListResponse>> HandleAsync(ListReadings query, CancellationToken cancellationToken)
    {
        var readings = readModel.All()
            .Where(snapshot => !snapshot.IsDeleted)
            .OrderBy(snapshot => snapshot.CreatedAt)
            .ToList();

        Result<ReadingListResponse> result = new ReadingListResponse("List", readings, []);
        return Task.FromResult(result);
    }
}
