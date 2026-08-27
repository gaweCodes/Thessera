using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;
using Microsoft.EntityFrameworkCore;

namespace StateStoredWithMessaging;

public sealed class ListReadingsHandler(ReadingDbContext context) : IQueryHandler<ListReadings, ReadingListResponse>
{
    public async Task<Result<ReadingListResponse>> HandleAsync(ListReadings query, CancellationToken cancellationToken)
    {
        var readings = await context.Readings
            .AsNoTracking()
            .Where(state => !state.IsDeleted)
            .OrderBy(state => state.CreatedAt)
            .Select(state => ReadingSnapshot.From(state))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ReadingListResponse("List", readings, []);
    }
}
