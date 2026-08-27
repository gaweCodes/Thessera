using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;

namespace EventSourced;

public sealed class DeleteReadingHandler(IRepository<Reading, ReadingId> repository)
    : ICommandHandler<DeleteReading, ReadingOperationResponse>
{
    public async Task<Result<ReadingOperationResponse>> HandleAsync(DeleteReading command, CancellationToken cancellationToken)
    {
        var reading = await repository.GetByIdAsync(new ReadingId(command.Id), cancellationToken).ConfigureAwait(false);
        if (reading is null || reading.IsDeleted)
        {
            return Failure.NotFound("reading.not_found", "Reading not found.");
        }

        reading.Delete();
        return ReadingResponseFactory.ForMutation("Delete", reading);
    }
}
