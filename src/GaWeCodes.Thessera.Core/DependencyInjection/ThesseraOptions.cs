using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Registration;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Dispatching;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using GaWeCodes.Thessera.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.DependencyInjection;

public sealed class ThesseraOptions
{
    public const int LoggingBehaviorOrder = 0;

    public const int ExceptionToResultBehaviorOrder = 100;

    public const int UnitOfWorkBehaviorOrder = 300;

    private readonly DomainEventCatalog _domainEvents = new();
    private readonly HandlerRegistrar _handlers;
    private readonly PersistenceRegistrar _persistence;
    private readonly MessagingRegistrar _messaging;
    private readonly ProvisioningRegistrar _provisioning;

    internal ThesseraOptions(IServiceCollection services, PipelineBehaviorRegistry behaviorRegistry)
    {
        _handlers = new HandlerRegistrar(services, behaviorRegistry);
        _persistence = new PersistenceRegistrar(services, Wiring.Persistence, Wiring.Provisioning, Wiring.Runtime);
        _messaging = new MessagingRegistrar(services, Wiring.Messaging, Wiring.Provisioning, Wiring.Runtime);
        _provisioning = new ProvisioningRegistrar(Wiring.Provisioning);
    }

    internal ThesseraWiringSettings Wiring { get; } = new();

    public RuntimeActivation Runtime => Wiring.Runtime;

    internal IReadOnlyCollection<Assembly> ScannedAssemblies => _handlers.ScannedAssemblies;

    internal IReadOnlyCollection<Assembly> DomainEventAssemblies => _domainEvents.Assemblies;

    internal DomainEventTypeRegistry DomainEventTypeRegistry => _domainEvents.Registry;

    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public ThesseraOptions AddHandlersFrom(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _handlers.AddFrom(assembly);
        return this;
    }

    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public ThesseraOptions AddDomainEventsFrom(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _domainEvents.Add(assembly);
        return this;
    }

    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public ThesseraOptions AddPipelineBehavior(Type openGenericBehavior, int order)
    {
        ArgumentNullException.ThrowIfNull(openGenericBehavior);

        _handlers.AddPipelineBehavior(openGenericBehavior, order);
        return this;
    }

    public ThesseraOptions UseNoPersistence()
    {
        _persistence.UseNone();
        return this;
    }

    public ThesseraOptions UsePersistence(IPersistenceAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        _persistence.Use(adapter);
        return this;
    }

    public ThesseraOptions WithoutEventHistory()
    {
        Wiring.Persistence.WaiveEventHistory();
        return this;
    }

    public ThesseraOptions UseMessagingTransport(IMessagingTransportAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        _messaging.UseTransport(adapter);
        return this;
    }

    public ThesseraOptions SubscribeToIntegrationEvents(
        string endpointName,
        Assembly consumerAssembly,
        params string[] topicPatterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(consumerAssembly);
        ArgumentNullException.ThrowIfNull(topicPatterns);

        _messaging.Subscribe(endpointName, consumerAssembly, topicPatterns);
        return this;
    }

    public ThesseraOptions ProvisionInfrastructure(InfrastructureProvisioning provisioning)
    {
        if (!Enum.IsDefined(provisioning))
        {
            throw new ArgumentOutOfRangeException(nameof(provisioning), provisioning, null);
        }

        _provisioning.Select(provisioning);
        return this;
    }
}
