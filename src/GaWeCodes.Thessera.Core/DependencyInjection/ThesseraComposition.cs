using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Validation;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Dispatching;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Core.Time;
using GaWeCodes.Thessera.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GaWeCodes.Thessera.Core.DependencyInjection;

internal static class ThesseraComposition
{
    public static ThesseraWiringSettings Compose(IServiceCollection services, Action<ThesseraOptions> configure)
    {
        EnsureSingleCall(services);

        var behaviorRegistry = new PipelineBehaviorRegistry();
        var options = Configure(services, behaviorRegistry, configure);

        Validate(options);
        RegisterCore(services, options);
        ValidateBehaviorOrders(services, behaviorRegistry);
        RegisterStartupChecks(services, options);

        return options.Wiring;
    }

    private static void EnsureSingleCall(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ThesseraRegistrationMarker)))
        {
            throw new InvalidOperationException(
                "AddThessera was called more than once on the same service collection. The behavior " +
                "registry, the Wolverine wiring settings and the domain event names are one shared object each, " +
                "registered by the first call; a second call would fill fresh instances that are never resolved, " +
                "so its behaviors would run at order 0, its persistence and messaging selection would be ignored " +
                "and its [EventName] names would be missing at the first commit. Make every selection in a single " +
                "AddThessera callback.");
        }

        services.AddSingleton(new ThesseraRegistrationMarker());
    }

    private static ThesseraOptions Configure(
        IServiceCollection services,
        PipelineBehaviorRegistry behaviorRegistry,
        Action<ThesseraOptions> configure)
    {
        services.AddSingleton(behaviorRegistry);
        services.TryAddSingleton<IIntegrationEventSinkFactory, NullIntegrationEventSinkFactory>();

        var options = new ThesseraOptions(services, behaviorRegistry);
        configure(options);
        return options;
    }

    private static void Validate(ThesseraOptions options)
    {
        var wiring = options.Wiring;

        if (wiring.Messaging.Subscription is not null && wiring.Messaging.Transport is null)
        {
            throw new InvalidOperationException(
                "SubscribeToIntegrationEvents was selected without a messaging transport. Subscribing declares a " +
                "listening endpoint on the broker and binds it to the topics it wants, so a transport must be " +
                "selected as well.");
        }

        if (wiring.Messaging.IsSelected && !wiring.Persistence.IsSelected)
        {
            throw new InvalidOperationException(
                "A messaging transport was selected without a persistence strategy. Integration events are sent " +
                "through a durable endpoint so that they survive a broker restart and a crash between commit and " +
                "broker acknowledgement, and a durable endpoint needs Wolverine's message store. " +
                "Without one the host would look durable and silently not be. Select UseEfCoreStateStore<TContext>" +
                "(writeConnectionString) or UseMartenEventStore(writeConnectionString) as well.");
        }

        if (!wiring.Persistence.IsSelected)
        {
            return;
        }

        if (options.DomainEventTypeRegistry.NamesByType.Count == 0)
        {
            throw new InvalidOperationException(
                "A persistence strategy was configured but no domain event assembly was registered. Every domain " +
                "event is written to the outbox under the name from its [EventName], so the names must be known " +
                "before the first commit: call options.AddDomainEventsFrom(typeof(SomeDomainEvent).Assembly).");
        }

        AggregateFactory.EnsureAggregatesAreReconstitutable(options.DomainEventAssemblies);
    }

    private static void ValidateBehaviorOrders(IServiceCollection services, PipelineBehaviorRegistry behaviorRegistry)
    {
        foreach (var descriptor in services)
        {
            var serviceType = descriptor.ServiceType;
            if (!serviceType.IsGenericType || serviceType.GetGenericTypeDefinition() != typeof(IPipelineBehavior<,>))
            {
                continue;
            }

            var implementationType = descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
            if (implementationType is not null && behaviorRegistry.TryGetOrder(implementationType, out _))
            {
                continue;
            }

            var name = implementationType?.ToString() ?? $"a factory-registered behavior for '{serviceType}'";

            throw new InvalidOperationException(
                $"The pipeline behavior {name} was added to the service collection directly and therefore has no " +
                "order. An unordered behavior would run at order 0, which is the order of the logging behavior, so " +
                "it would silently collide with it. Register it with " +
                "options.AddPipelineBehavior(typeof(MyBehavior<,>), order) instead.");
        }
    }

    private static void RegisterCore(IServiceCollection services, ThesseraOptions options)
    {
        services.AddSingleton(options.DomainEventTypeRegistry);
        services.TryAddSingleton<DomainEventEnvelopeSerializer>();

        services.AddSingleton(options.Wiring);
        services.AddSingleton<IWiringSnapshot>(options.Wiring);
        services.AddSingleton(options.Wiring.Persistence);
        services.AddSingleton(options.Wiring.Messaging);
        services.AddSingleton(options.Wiring.Provisioning);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ISender, RequestSender>();

        options.AddPipelineBehavior(typeof(LoggingBehavior<,>), ThesseraOptions.LoggingBehaviorOrder);
        options.AddPipelineBehavior(typeof(ExceptionToResultBehavior<,>), ThesseraOptions.ExceptionToResultBehaviorOrder);
        options.AddPipelineBehavior(typeof(UnitOfWorkBehavior<,>), ThesseraOptions.UnitOfWorkBehaviorOrder);

        services.TryAddScoped<ProjectionRunner>();
        services.TryAddScoped<MapperRunner>();
        services.TryAddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();
        services.TryAddScoped<IUnitOfWork, NullUnitOfWork>();
    }

    private static void RegisterStartupChecks(IServiceCollection services, ThesseraOptions options)
    {
        services.AddHostedService<StartupCheckRunner>();

        services.AddSingleton<IStartupCheck>(provider =>
            new HandlerRegistrationCheck(provider, options.ScannedAssemblies));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupCheck, IntegrationEventMapperCheck>());
        services.AddSingleton<IStartupCheck>(provider => new UnitOfWorkPresenceCheck(
            provider,
            options.Wiring.Persistence,
            options.ScannedAssemblies,
            provider.GetRequiredService<ILogger<UnitOfWorkPresenceCheck>>()));
        services.AddSingleton<IStartupCheck>(new AggregatePersistenceMatchCheck(
            options.Wiring.Persistence,
            services));
        services.AddSingleton<IStartupCheck>(new AggregateStateSelfBindingCheck(
            [.. options.ScannedAssemblies.Union(options.DomainEventAssemblies)]));
    }
}
