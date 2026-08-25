using System.Collections.Concurrent;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

internal sealed class MapperRunner(IServiceProvider serviceProvider)
{
    private static readonly ConcurrentDictionary<Type, MapperInvoker> Invokers = new();

    public Task<int> RunAsync(
        IDomainEvent domainEvent,
        DomainEventMetadata metadata,
        IIntegrationEventSink integrationEventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(integrationEventSink);

        var invoker = Invokers.GetOrAdd(
            domainEvent.GetType(),
            static type => (MapperInvoker)Activator.CreateInstance(
                typeof(MapperInvoker<>).MakeGenericType(type))!);

        return invoker.InvokeAsync(domainEvent, metadata, serviceProvider, integrationEventSink, cancellationToken);
    }

    private abstract class MapperInvoker
    {
        public abstract Task<int> InvokeAsync(
            IDomainEvent domainEvent,
            DomainEventMetadata metadata,
            IServiceProvider services,
            IIntegrationEventSink integrationEventSink,
            CancellationToken cancellationToken);
    }

    private sealed class MapperInvoker<TDomainEvent> : MapperInvoker
        where TDomainEvent : IDomainEvent
    {
        public override async Task<int> InvokeAsync(
            IDomainEvent domainEvent,
            DomainEventMetadata metadata,
            IServiceProvider services,
            IIntegrationEventSink integrationEventSink,
            CancellationToken cancellationToken)
        {
            var typedEvent = (TDomainEvent)domainEvent;
            var published = 0;

            foreach (var mapper in services.GetServices<IIntegrationEventMapper<TDomainEvent>>())
            {
                foreach (var integrationEvent in mapper.Map(typedEvent, metadata))
                {
                    await integrationEventSink.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
                    published++;
                }
            }

            return published;
        }
    }
}
