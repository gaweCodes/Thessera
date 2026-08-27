using GaWeCodes.Thessera.Application.Results;

namespace DomainApplication;

public sealed class ReadingApplication
{
    private readonly InMemoryReadingRepository _repository = new();
    private readonly InMemoryReadingIdSequence _idSequence = new();

    public Task<Result<ReadingOperationResponse>> CreateAsync(int value, CancellationToken cancellationToken = default) =>
        new CreateReadingHandler(_repository, _idSequence).HandleAsync(new CreateReading(value), cancellationToken);

    public Task<Result<ReadingListResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        new ListReadingsHandler(_repository).HandleAsync(new ListReadings(), cancellationToken);

    public Task<Result<ReadingOperationResponse>> UpdateAsync(int id, int value, CancellationToken cancellationToken = default) =>
        new UpdateReadingHandler(_repository).HandleAsync(new UpdateReading(id, value), cancellationToken);

    public Task<Result<ReadingOperationResponse>> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        new DeleteReadingHandler(_repository).HandleAsync(new DeleteReading(id), cancellationToken);
}
