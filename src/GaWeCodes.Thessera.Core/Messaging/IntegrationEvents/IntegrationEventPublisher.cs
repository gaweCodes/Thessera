using System.Diagnostics;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Telemetry;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

internal sealed class IntegrationEventPublisher(MapperRunner mapperRunner) : IIntegrationEventPublisher
{
    public async Task PublishAsync(IDomainEvent domainEvent, DomainEventMetadata metadata, IIntegrationEventSink integrationEventSink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(integrationEventSink);

        if (!ThesseraTelemetry.Source.HasListeners())
        {
            await DispatchAsync(domainEvent, metadata, integrationEventSink, cancellationToken).ConfigureAwait(false);
            return;
        }

        var domainEventName = domainEvent.GetType().Name;
        using var activity = ThesseraTelemetry.Source.StartActivity(
            $"Publish {domainEventName}",
            ActivityKind.Internal);

        activity?.SetTag(TelemetryTags.DomainEventName, domainEventName);
        activity?.SetTag(TelemetryTags.AggregateName, metadata.AggregateName);
        activity?.SetTag(TelemetryTags.AggregateId, metadata.AggregateId);
        activity?.SetTag(TelemetryTags.AggregateVersion, metadata.Version);

        try
        {
            var published = await DispatchAsync(domainEvent, metadata, integrationEventSink, cancellationToken)
                .ConfigureAwait(false);

            activity?.SetTag(TelemetryTags.IntegrationEventsPublished, published);
            activity?.MarkSucceeded();
        }
        catch (Exception exception)
        {
            activity?.MarkFaulted(exception);
            throw;
        }
    }

    private Task<int> DispatchAsync(
        IDomainEvent domainEvent,
        DomainEventMetadata metadata,
        IIntegrationEventSink integrationEventSink,
        CancellationToken cancellationToken)
        => mapperRunner.RunAsync(domainEvent, metadata, integrationEventSink, cancellationToken);
}
