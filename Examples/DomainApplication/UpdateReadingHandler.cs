using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;

namespace DomainApplication;

public sealed class UpdateReadingHandler(IRepository<Reading, ReadingId> repository)
    : ICommandHandler<UpdateReading, ReadingOperationResponse>
{
    public async Task<Result<ReadingOperationResponse>> HandleAsync(UpdateReading command, CancellationToken cancellationToken)
    {
        var reading = await repository.GetByIdAsync(new ReadingId(command.Id), cancellationToken).ConfigureAwait(false);
        if (reading is null || reading.IsDeleted)
        {
            return Failure.NotFound("reading.not_found", "Reading not found.");
        }

        try
        {
            reading.ChangeValue(command.Value);
            return ReadingResponseFactory.ForMutation("Update", reading);
        }
        catch (DomainValidationException exception)
        {
            return Failure.Validation(exception.Violations[0].Code, exception.Message);
        }
    }
}
