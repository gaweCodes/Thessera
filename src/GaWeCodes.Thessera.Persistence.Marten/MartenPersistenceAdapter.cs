using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Persistence.Marten.ReadModels;
using GaWeCodes.Thessera.Npgsql;
using GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Wolverine.Diagnostics;
using JasperFx;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine.Marten;

namespace GaWeCodes.Thessera.Persistence.Marten;

internal sealed record MartenPersistenceAdapter(string WriteConnectionString) : IPersistenceAdapter
{
    public string Description => "UseMartenEventStore";

    public AggregateStyle AggregateStyle => AggregateStyle.EventSourced;

    public bool IsTransientFault(Exception exception) => PostgresTransientFaults.IsTransient(exception);

    public void Register(PersistenceRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.Services;
        var connectionString = WriteConnectionString;

        services.AddMarten(serviceProvider =>
        {
            var storeOptions = new StoreOptions();
            storeOptions.Connection(connectionString);
            storeOptions.AutoCreateSchemaObjects = context.ProvisionsInfrastructure
                ? AutoCreate.CreateOrUpdate
                : AutoCreate.None;
            storeOptions.Events.StreamIdentity = StreamIdentity.AsString;
            storeOptions.UseSystemTextJsonForSerialization(EntityKeyJsonOptions.Create());

            foreach (var (domainEventType, eventName) in serviceProvider
                .GetRequiredService<DomainEventTypeRegistry>()
                .NamesByType)
            {
                storeOptions.Events.MapEventType(domainEventType, eventName);
            }

            return storeOptions;
        }).UseLightweightSessions()
            .IntegrateWithWolverine();

        services.TryAddScoped<MartenAggregateTracker>();
        services.TryAddSingleton<DomainEventEnvelopeFactory>();
        services.TryAddSingleton<EventSourcedReadModelRebuildRunner>();

        if (context.ClaimedAggregates.Count == 0)
        {
            services.TryAddScoped<IUnitOfWork, MartenUnitOfWork>();
            services.TryAddScoped(typeof(IRepository<,>), typeof(MartenEventSourcedRepository<,>));
        }
        else
        {
            services.TryAddKeyedScoped<IUnitOfWork, MartenUnitOfWork>(context.StoreId);

            foreach (var aggregateType in context.ClaimedAggregates)
            {
                var keyType = AggregateKeyType.Of(aggregateType);

                if (!IsEventSourced(aggregateType, keyType))
                {
                    continue;
                }

                services.TryAddScoped(
                    typeof(IRepository<,>).MakeGenericType(aggregateType, keyType),
                    typeof(MartenEventSourcedRepository<,>).MakeGenericType(aggregateType, keyType));
            }
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPersistenceFaultTranslator, MartenFaultTranslator>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPersistenceFaultTranslator, PostgresFaultTranslator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupCheck, MartenSchemaProvisioner>(
            provider => new MartenSchemaProvisioner(provider, () => context.ProvisionsInfrastructure)));

        context.UseWolverineRuntime().TryClaimMainMessageStore();
        DeadLetterHealthCheckRegistration.Register(services);
    }

    private static bool IsEventSourced(Type aggregateType, Type keyType) =>
        typeof(IEventSourcedAggregateRoot<>).MakeGenericType(keyType).IsAssignableFrom(aggregateType);
}
