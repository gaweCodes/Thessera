using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Core.Startup;
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
        services.TryAddScoped<IUnitOfWork, MartenUnitOfWork>();
        services.TryAddScoped(typeof(IRepository<,>), typeof(MartenEventSourcedRepository<,>));
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPersistenceFaultTranslator, MartenFaultTranslator>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPersistenceFaultTranslator, PostgresFaultTranslator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupCheck, MartenSchemaProvisioner>(
            provider => new MartenSchemaProvisioner(provider, () => context.ProvisionsInfrastructure)));
        context.UseWolverineRuntime();
        DeadLetterHealthCheckRegistration.Register(services);
    }
}
