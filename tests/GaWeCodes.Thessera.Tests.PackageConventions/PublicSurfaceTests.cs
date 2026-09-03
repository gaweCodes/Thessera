using System.Reflection;
using GaWeCodes.Thessera.Persistence.EfCore.StateStored;
using GaWeCodes.Thessera.Npgsql;
using GaWeCodes.Thessera.Testing;
using GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;

namespace GaWeCodes.Thessera.Tests;

public sealed class PublicSurfaceTests
{
    private static readonly Assembly Core = typeof(ServiceCollectionExtensions).Assembly;

    private static readonly Assembly WolverineAdapter = typeof(WolverineRuntimeRegistration).Assembly;

    private static readonly Assembly NpgsqlFaults = typeof(PostgresTransientFaults).Assembly;

    private static readonly Assembly EfCore = typeof(IEfCoreDatabaseDriver).Assembly;

    private static readonly Assembly EfCorePostgres = typeof(EfCoreStateStoreExtensions).Assembly;

    private static readonly Assembly Marten = typeof(MartenEventStoreExtensions).Assembly;

    private static readonly Assembly RabbitMq = typeof(RabbitMqMessagingExtensions).Assembly;

    private static readonly Assembly Testing = typeof(PersistedSchema).Assembly;

    private static readonly Assembly[] AllAssemblies =
        [Core, WolverineAdapter, NpgsqlFaults, EfCore, EfCorePostgres, Marten, RabbitMq, Testing];

    private static readonly string[] IntendedCoreApi =
    [
        "GaWeCodes.Thessera.Core.DependencyInjection.ThesseraOptions",
        "GaWeCodes.Thessera.HostApplicationBuilderExtensions",
        "GaWeCodes.Thessera.Core.DependencyInjection.InfrastructureProvisioning",
        "GaWeCodes.Thessera.ServiceCollectionExtensions",
        "GaWeCodes.Thessera.Core.Persistence.EntityKeyJsonOptions",
        "GaWeCodes.Thessera.Core.Persistence.IPersistenceFaultTranslator",
        "GaWeCodes.Thessera.Core.Startup.IStartupCheck",
        "GaWeCodes.Thessera.Core.Startup.StartupPhase",
    ];

    private static readonly string[] IntendedCoreAdapterContract =
    [
        "GaWeCodes.Thessera.Core.DependencyInjection.Extensibility.IRuntimeActivator",
        "GaWeCodes.Thessera.Core.DependencyInjection.Extensibility.IWiringSnapshot",
        "GaWeCodes.Thessera.Core.DependencyInjection.Extensibility.RuntimeActivation",
        "GaWeCodes.Thessera.Core.DependencyInjection.Wiring.IntegrationEventSubscription",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.DomainEventMetadataFactory",
        "GaWeCodes.Thessera.Core.Messaging.IntegrationEvents.TopicPatternMatcher",
        "GaWeCodes.Thessera.Core.Messaging.IntegrationEvents.TopicResolver",
        "GaWeCodes.Thessera.Core.Messaging.Transport.IMessageEmitter",
        "GaWeCodes.Thessera.Core.Messaging.Transport.IMessagingTransportAdapter",
        "GaWeCodes.Thessera.Core.Messaging.Transport.MessagingTransportRegistrationContext",
        "GaWeCodes.Thessera.Core.Persistence.AggregateFactory",
        "GaWeCodes.Thessera.Core.Persistence.AggregateKeyType",
        "GaWeCodes.Thessera.Core.Persistence.AggregateStyle",
        "GaWeCodes.Thessera.Core.Persistence.EntityKeyActivator",
        "GaWeCodes.Thessera.Core.Persistence.IPersistenceAdapter",
        "GaWeCodes.Thessera.Core.Persistence.PersistenceRegistrationContext",
        "GaWeCodes.Thessera.Core.Startup.SynchronousStartupCheck",
    ];

    private static readonly string[] IntendedTestingApi =
    [
        "GaWeCodes.Thessera.Testing.AggregateConventions",
        "GaWeCodes.Thessera.Testing.PersistedSchema",
        "GaWeCodes.Thessera.Testing.TestMetadata",
    ];

    private static readonly string[] IntendedWolverineApi =
    [
        "GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring.WolverineRuntimeActivator",
        "GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring.WolverineRuntimeRegistration",
        "GaWeCodes.Thessera.WolverineRuntimeOptionsExtensions",
        "GaWeCodes.Thessera.Wolverine.Diagnostics.DeadLetterHealthCheckRegistration",
        "GaWeCodes.Thessera.Wolverine.Messaging.Transport.IWolverineMessagingTransport",
        "GaWeCodes.Thessera.Wolverine.Persistence.IOutboxDurabilityConfigurator",
    ];

    // What a store author writes against. This was its own package until the store toolkit was
    // folded into the core; it stays a separate list because it is a distinct promise.
    private static readonly string[] IntendedCoreStoreAuthorApi =
    [
        "GaWeCodes.Thessera.Core.Persistence.AggregateTracker`1",
        "GaWeCodes.Thessera.Core.Persistence.DomainEventEnvelopeFactory",
        "GaWeCodes.Thessera.Core.Persistence.EntityKeyFormatter",
        "GaWeCodes.Thessera.Core.Persistence.ITrackedAggregate",
        "GaWeCodes.Thessera.Core.Persistence.PersistenceFailureCodes",
        "GaWeCodes.Thessera.Core.ReadModels.ReadModelRebuildWriter",
    ];

    private static readonly string[] IntendedNpgsqlApi =
    [
        "GaWeCodes.Thessera.Npgsql.PostgresFaultTranslator",
        "GaWeCodes.Thessera.Npgsql.PostgresTransientFaults",
    ];

    private static readonly string[] IntendedEfCoreApi =
    [
        "GaWeCodes.Thessera.Persistence.EfCore.EntityKeyModelBuilderExtensions",
        "GaWeCodes.Thessera.Persistence.EfCore.StateStored.EfCorePersistenceAdapter`1",
        "GaWeCodes.Thessera.Persistence.EfCore.StateStored.IEfCoreDatabaseDriver",
        "GaWeCodes.Thessera.Persistence.EfCore.ReadModels.StateStoredReadModelRebuildRunner`1",
    ];

    private static readonly string[] IntendedEfCorePostgresApi =
    [
        "GaWeCodes.Thessera.EfCoreStateStoreExtensions",
    ];

    private static readonly string[] IntendedMartenApi =
    [
        "GaWeCodes.Thessera.MartenEventStoreExtensions",
        "GaWeCodes.Thessera.Persistence.Marten.ReadModels.EventSourcedReadModelRebuildRunner",
    ];

    private static readonly string[] IntendedRabbitMqApi =
    [
        "GaWeCodes.Thessera.RabbitMqMessagingExtensions",
    ];

    private static readonly string[] ExtensionPoints =
    [
        "GaWeCodes.Thessera.Core.DependencyInjection.Extensibility.IRuntimeActivator",
        "GaWeCodes.Thessera.Core.DependencyInjection.Extensibility.IWiringSnapshot",
        "GaWeCodes.Thessera.Core.Messaging.Transport.IMessagingTransportAdapter",
        "GaWeCodes.Thessera.Wolverine.Messaging.Transport.IWolverineMessagingTransport",
        "GaWeCodes.Thessera.Core.Persistence.AggregateStyle",
        "GaWeCodes.Thessera.Wolverine.Persistence.IOutboxDurabilityConfigurator",
        "GaWeCodes.Thessera.Core.Persistence.IPersistenceAdapter",
        "GaWeCodes.Thessera.Core.Persistence.IPersistenceFaultTranslator",
        "GaWeCodes.Thessera.Core.Startup.IStartupCheck",
        "GaWeCodes.Thessera.Core.Startup.StartupPhase",
        "GaWeCodes.Thessera.Core.Startup.SynchronousStartupCheck",
    ];

    private static readonly string[] RequiredByWolverineCodeGeneration =
    [
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.DomainEventEnvelope",
        "GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents.DomainEventEnvelopeHandler",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.DomainEventEnvelopeSerializer",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.DomainEventTypeRegistry",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.ProjectionEnvelope",
        "GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents.ProjectionEnvelopeHandler",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.ProjectionRunner",
        "GaWeCodes.Thessera.Core.Messaging.IntegrationEvents.IIntegrationEventSinkFactory",
        "GaWeCodes.Thessera.Core.Messaging.IntegrationEvents.IntegrationEventSourceContext",
        "GaWeCodes.Thessera.Wolverine.Messaging.IntegrationEvents.OwnContextIntegrationEventFilter",
    ];

    private static readonly string[] CodeGenerationTypesInTheCore =
    [
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.DomainEventEnvelope",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.DomainEventEnvelopeSerializer",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.DomainEventTypeRegistry",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.ProjectionEnvelope",
        "GaWeCodes.Thessera.Core.Messaging.DomainEvents.ProjectionRunner",
        "GaWeCodes.Thessera.Core.Messaging.IntegrationEvents.IIntegrationEventSinkFactory",
        "GaWeCodes.Thessera.Core.Messaging.IntegrationEvents.IntegrationEventSourceContext",
    ];

    private static readonly string[] CodeGenerationTypesInTheWolverineAdapter =
    [
        "GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents.DomainEventEnvelopeHandler",
        "GaWeCodes.Thessera.Wolverine.Messaging.DomainEvents.ProjectionEnvelopeHandler",
        "GaWeCodes.Thessera.Wolverine.Messaging.IntegrationEvents.OwnContextIntegrationEventFilter",
    ];

    public static TheoryData<string, string[]> PinnedSurfaces =>
        new()
        {
            {
                "GaWeCodes.Thessera.Core",
                [.. IntendedCoreApi
                    .Concat(IntendedCoreAdapterContract)
                    .Concat(IntendedCoreStoreAuthorApi)
                    .Concat(CodeGenerationTypesInTheCore)]
            },
            { "GaWeCodes.Thessera.Testing", IntendedTestingApi },
            {
                "GaWeCodes.Thessera.Wolverine",
                [.. IntendedWolverineApi.Concat(CodeGenerationTypesInTheWolverineAdapter)]
            },
            { "GaWeCodes.Thessera.Npgsql", IntendedNpgsqlApi },
            { "GaWeCodes.Thessera.Persistence.EfCore", IntendedEfCoreApi },
            { "GaWeCodes.Thessera.Persistence.EfCore.Postgres", IntendedEfCorePostgresApi },
            { "GaWeCodes.Thessera.Persistence.Marten", IntendedMartenApi },
            { "GaWeCodes.Thessera.Messaging.RabbitMq", IntendedRabbitMqApi },
        };

    [Theory]
    [MemberData(nameof(PinnedSurfaces))]
    public void ThePublicSurface_IsExactlyTheIntendedApiPlusWhatCodeGenerationForces(
        string assemblyName,
        string[] intended)
    {
        ArgumentNullException.ThrowIfNull(intended);

        var expected = intended.Order(StringComparer.Ordinal).ToArray();
        var actual = AssemblyNamed(assemblyName)
            .GetExportedTypes()
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NoInfrastructureImplementationIsPublic()
    {
        var intended = IntendedCoreApi
            .Concat(IntendedCoreAdapterContract)
            .Concat(IntendedWolverineApi)
            .Concat(IntendedCoreStoreAuthorApi)
            .Concat(IntendedNpgsqlApi)
            .Concat(IntendedEfCoreApi)
            .Concat(IntendedEfCorePostgresApi)
            .Concat(IntendedMartenApi)
            .Concat(IntendedRabbitMqApi)
            .ToArray();

        var leaked = AllAssemblies
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Namespace is not null
                && (type.Namespace.Contains(".Persistence", StringComparison.Ordinal)
                    || type.Namespace.EndsWith(".Dispatching", StringComparison.Ordinal)
                    || type.Namespace.EndsWith(".Events", StringComparison.Ordinal)
                    || type.Namespace.EndsWith(".Time", StringComparison.Ordinal)
                    || type.Namespace.EndsWith(".Wiring", StringComparison.Ordinal)
                    || type.Namespace.EndsWith(".Registration", StringComparison.Ordinal)
                    || type.Namespace.EndsWith(".Startup", StringComparison.Ordinal)
                    || type.Namespace.EndsWith(".Validation", StringComparison.Ordinal)))
            .Select(type => type.FullName!)
            .Except(intended, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leaked);
    }

    [Fact]
    public void TheCoreNamesNoSatelliteAssembly()
    {
        var satellites = new[] { WolverineAdapter, EfCore, Marten, RabbitMq }
            .Select(assembly => assembly.GetName().Name!)
            .ToArray();

        var referenced = Core.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Intersect(satellites, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(referenced);
    }

    [Fact]
    public void EveryExtensionPointIsAnAbstraction_NotAnImplementation()
    {
        foreach (var name in ExtensionPoints)
        {
            var type = TypeNamed(name);
            Assert.NotNull(type);
            Assert.True(
                type.IsInterface || type.IsEnum || type.IsAbstract,
                $"'{name}' is offered as an extension point, so it must be an abstraction consumers can implement.");
        }
    }

    [Fact]
    public void EveryTypeExemptedForCodeGeneration_IsActuallyReachableFromGeneratedCode()
    {
        foreach (var name in RequiredByWolverineCodeGeneration)
        {
            var type = TypeNamed(name);
            Assert.NotNull(type);
            Assert.True(type.IsPublic, $"'{name}' is listed as code-generation exempt but is not public.");
        }
    }

    [Fact]
    public void TheAssemblyExposesNoPublicField()
    {
        var fields = AllAssemblies
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => !type.IsEnum)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(field => !field.IsLiteral)
            .Select(field => $"{field.DeclaringType?.FullName}.{field.Name}")
            .ToArray();

        Assert.Empty(fields);
    }

    private static Type? TypeNamed(string name) =>
        AllAssemblies.Select(assembly => assembly.GetType(name)).FirstOrDefault(type => type is not null);

    private static Assembly AssemblyNamed(string name) =>
        AllAssemblies.Single(assembly => string.Equals(assembly.GetName().Name, name, StringComparison.Ordinal));
}
