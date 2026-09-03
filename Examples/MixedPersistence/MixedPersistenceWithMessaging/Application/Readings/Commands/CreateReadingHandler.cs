using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistenceWithMessaging;

public sealed class CreateReadingHandler(IRepository<Reading, ReadingId> repository, IReadingIdSequence idSequence)
    : ICommandHandler<CreateReading, ReadingOperationResponse>
{
    public async Task<Result<ReadingOperationResponse>> HandleAsync(CreateReading command, CancellationToken cancellationToken)
    {
        var readingId = idSequence.ReserveNext();

        try
        {
            var reading = Reading.Record(readingId, command.Value);
            await repository.AddAsync(reading, cancellationToken).ConfigureAwait(false);
            return ReadingResponseFactory.ForMutation("Create", reading);
        }
        catch (DomainValidationException exception)
        {
            idSequence.TryRelease(readingId);
            return Failure.Validation(exception.Violations[0].Code, exception.Message);
        }
    }
}
