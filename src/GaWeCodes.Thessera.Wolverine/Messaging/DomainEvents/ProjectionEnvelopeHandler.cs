using GaWeCodes.Thessera.Core.Messaging.DomainEvents;

namespace GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;

public sealed class ProjectionEnvelopeHandler(
    ProjectionRunner projectionRunner,
    DomainEventEnvelopeSerializer serializer)
{
    public Task Handle(ProjectionEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var domainEvent = serializer.Unwrap(envelope.Event);
        var metadata = DomainEventMetadataFactory.From(envelope.Event);

        return projectionRunner.RunAsync(domainEvent, metadata, cancellationToken);
    }
}
