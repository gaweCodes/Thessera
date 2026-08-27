using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;

namespace DomainApplication;

public sealed class ListReadingsHandler(IReadingCatalog catalog) : IQueryHandler<ListReadings, ReadingListResponse>
{
    public async Task<Result<ReadingListResponse>> HandleAsync(ListReadings query, CancellationToken cancellationToken)
    {
        var readings = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        return new ReadingListResponse("List", readings, []);
    }
}
