using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Wolverine.Messaging.Transport;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents;

public sealed class DomainEventEnvelopeHandler(
    IIntegrationEventPublisher publisher,
    DomainEventEnvelopeSerializer serializer,
    IIntegrationEventSinkFactory sinkFactory)
{
    public async Task Handle(DomainEventEnvelope envelope, IMessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(context);

        var domainEvent = serializer.Unwrap(envelope);
        var metadata = DomainEventMetadataFactory.From(envelope);
        var emitter = new WolverineMessageEmitter(context);

        await publisher.PublishAsync(domainEvent, metadata, sinkFactory.Create(emitter), cancellationToken)
            .ConfigureAwait(false);

        await emitter.PublishAsync(new ProjectionEnvelope(envelope), null, cancellationToken).ConfigureAwait(false);
    }
}
