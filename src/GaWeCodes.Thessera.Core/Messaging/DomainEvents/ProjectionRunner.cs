using System.Collections.Concurrent;
using System.Diagnostics;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Core.Telemetry;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

/// <summary>
/// Dispatches one domain event to every projection handler registered for its type.
/// </summary>
/// <param name="serviceProvider">Resolves the handlers for the event being dispatched.</param>
/// <remarks>
/// Public so that a runtime adapter can drive it from its own queue handler; a consumer writes
/// projection handlers rather than calling this.
/// </remarks>
public sealed class ProjectionRunner(IServiceProvider serviceProvider)
{
    private static readonly ConcurrentDictionary<Type, ProjectionInvoker> Invokers = new();

    /// <summary>
    /// Runs every projection handler registered for this event.
    /// </summary>
    /// <param name="domainEvent">The event that happened.</param>
    /// <param name="metadata">Its context, handed to each handler alongside the event.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once every handler has run.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="domainEvent"/> or <paramref name="metadata"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Dispatch is by the event's runtime type, so a handler registered for a base type does not see
    /// a derived event. An event with no handler is not an error: most events are of interest to
    /// nobody's read model.
    /// </remarks>
    public Task RunAsync(IDomainEvent domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(metadata);

        var invoker = Invokers.GetOrAdd(
            domainEvent.GetType(),
            static type => (ProjectionInvoker)Activator.CreateInstance(
                typeof(ProjectionInvoker<>).MakeGenericType(type))!);

        return invoker.InvokeAsync(domainEvent, metadata, serviceProvider, cancellationToken);
    }

    private abstract class ProjectionInvoker
    {
        public abstract Task InvokeAsync(
            IDomainEvent domainEvent,
            DomainEventMetadata metadata,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    private sealed class ProjectionInvoker<TDomainEvent> : ProjectionInvoker
        where TDomainEvent : IDomainEvent
    {
        public override async Task InvokeAsync(
            IDomainEvent domainEvent,
            DomainEventMetadata metadata,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var typedEvent = (TDomainEvent)domainEvent;
            foreach (var handler in services.GetServices<IProjectionHandler<TDomainEvent>>())
            {
                await InvokeHandlerAsync(handler, typedEvent, metadata, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task InvokeHandlerAsync(
            IProjectionHandler<TDomainEvent> handler,
            TDomainEvent domainEvent,
            DomainEventMetadata metadata,
            CancellationToken cancellationToken)
        {
            if (!ThesseraTelemetry.Source.HasListeners())
            {
                await handler.HandleAsync(domainEvent, metadata, cancellationToken).ConfigureAwait(false);
                return;
            }

            var handlerName = handler.GetType().Name;
            using var activity = ThesseraTelemetry.Source.StartActivity(
                $"Project {handlerName}",
                ActivityKind.Internal);

            activity?.SetTag(TelemetryTags.ProjectionHandler, handler.GetType().FullName);
            activity?.SetTag(TelemetryTags.DomainEventName, typeof(TDomainEvent).Name);
            activity?.SetTag(TelemetryTags.AggregateName, metadata.AggregateName);
            activity?.SetTag(TelemetryTags.AggregateId, metadata.AggregateId);
            activity?.SetTag(TelemetryTags.AggregateVersion, metadata.Version);

            try
            {
                await handler.HandleAsync(domainEvent, metadata, cancellationToken).ConfigureAwait(false);
                activity?.MarkSucceeded();
            }
            catch (Exception exception)
            {
                activity?.MarkFaulted(exception);
                throw;
            }
        }
    }
}
