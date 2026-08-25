using GaWeCodes.Thessera.Application.DomainEvents;

namespace ContractsHost;

public sealed record ReadingSummary(string AggregateId, int Value, long Version);

public interface IReadingSummaryStore
{
    Task WriteAsync(ReadingSummary summary, CancellationToken cancellationToken);
}

public sealed class ReadingSummaryProjection(IReadingSummaryStore store)
    : IProjectionHandler<ReadingRecorded>, IProjectionHandler<ReadingCorrected>
{
    public Task HandleAsync(
        ReadingRecorded domainEvent,
        DomainEventMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);

        return store.WriteAsync(
            new ReadingSummary(metadata.AggregateId, domainEvent.Value, metadata.Version),
            cancellationToken);
    }

    public Task HandleAsync(
        ReadingCorrected domainEvent,
        DomainEventMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);

        return store.WriteAsync(
            new ReadingSummary(metadata.AggregateId, domainEvent.Value, metadata.Version),
            cancellationToken);
    }
}
