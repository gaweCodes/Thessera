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

/// <summary>
/// Everything a host decides about Thessera, in one place: what to scan, which store, which
/// transport, what to subscribe to, and whether it may touch infrastructure.
/// </summary>
/// <remarks>
/// Configured once inside <c>AddThessera</c>. Every satellite package contributes through a
/// <c>Use*</c> extension on this type rather than through a registration call of its own, and all of
/// them live in the one <c>GaWeCodes.Thessera</c> namespace — for the same reason
/// <c>AddConsole()</c> lives in <c>Microsoft.Extensions.Logging</c> rather than in
/// <c>…Logging.Console</c>.
/// </remarks>
public sealed class ThesseraOptions
{
    /// <summary>
    /// The order of the built-in logging behaviour: outermost, so it sees every request and every
    /// outcome including those a later behaviour produced.
    /// </summary>
    public const int LoggingBehaviorOrder = 0;

    /// <summary>
    /// The order of the built-in behaviour that turns domain exceptions into failed results. Inside
    /// logging, outside the unit of work.
    /// </summary>
    public const int ExceptionToResultBehaviorOrder = 100;

    /// <summary>
    /// The order of the built-in unit-of-work behaviour: innermost of the three, so it commits
    /// closest to the handler and its own failures still pass outwards through the other two.
    /// </summary>
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

    /// <summary>
    /// Gets the holder of the one runtime this host activates.
    /// </summary>
    /// <remarks>
    /// For adapter and runtime authors. A consumer reaches the runtime through the package that
    /// needs it, not through this.
    /// </remarks>
    public RuntimeActivation Runtime => Wiring.Runtime;

    internal IReadOnlyCollection<Assembly> ScannedAssemblies => _handlers.ScannedAssemblies;

    internal IReadOnlyCollection<Assembly> DomainEventAssemblies => _domainEvents.Assemblies;

    internal DomainEventTypeRegistry DomainEventTypeRegistry => _domainEvents.Registry;

    /// <summary>
    /// Scans an assembly for command and query handlers, pipeline behaviours, projection handlers
    /// and integration-event mappers.
    /// </summary>
    /// <param name="assembly">The assembly to scan, named through a type in it.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Call it once per assembly that holds any of those. Scanning is why the family is not
    /// trim-safe: on a trimmed build the types are gone and discovery silently finds nothing.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public ThesseraOptions AddHandlersFrom(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _handlers.AddFrom(assembly);
        return this;
    }

    /// <summary>
    /// Scans an assembly for domain events and records the persisted name of each.
    /// </summary>
    /// <param name="assembly">The assembly declaring the domain events.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A domain event in the assembly has no persisted name, or two of them claim the same one. Both
    /// are refused here rather than at the first write.
    /// </exception>
    /// <remarks>
    /// Not optional once a store is selected: every event is written under this name, and one that
    /// is unknown cannot be read back.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public ThesseraOptions AddDomainEventsFrom(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _domainEvents.Add(assembly);
        return this;
    }

    /// <summary>
    /// Adds a cross-cutting behaviour of your own around every dispatched request.
    /// </summary>
    /// <param name="openGenericBehavior">
    /// The open generic behaviour type, as <c>typeof(MyBehavior&lt;,&gt;)</c>.
    /// </param>
    /// <param name="order">
    /// Where it sits. Lower runs further out; the built-in three are at
    /// <see cref="LoggingBehaviorOrder"/>, <see cref="ExceptionToResultBehaviorOrder"/> and
    /// <see cref="UnitOfWorkBehaviorOrder"/>, so yours can be placed relative to them by name.
    /// </param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="openGenericBehavior"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public ThesseraOptions AddPipelineBehavior(Type openGenericBehavior, int order)
    {
        ArgumentNullException.ThrowIfNull(openGenericBehavior);

        _handlers.AddPipelineBehavior(openGenericBehavior, order);
        return this;
    }

    /// <summary>
    /// States that this host deliberately commits nothing.
    /// </summary>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// Not ceremony: it is the difference between "no store has been chosen yet" and "this host wants
    /// none", and only the second is safe to start. Without it, a host whose scanned assemblies
    /// contain commands fails at startup, because every one of those commands would report success
    /// while nothing was committed. Cannot be combined with a store.
    /// </remarks>
    public ThesseraOptions UseNoPersistence()
    {
        _persistence.UseNone();
        return this;
    }

    /// <summary>
    /// Selects the store for this host.
    /// </summary>
    /// <param name="adapter">The store announcing itself.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="adapter"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A different store, or <see cref="UseNoPersistence"/>, was already selected. A host has exactly
    /// one store: a bounded context has one write database, and a commit cannot span two.
    /// </exception>
    /// <remarks>
    /// Normally reached through a store package entry point rather than called directly.
    /// </remarks>
    public ThesseraOptions UsePersistence(IPersistenceAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        _persistence.Use(adapter);
        return this;
    }

    /// <summary>
    /// Selects an additional store for this host, owning only the named aggregates.
    /// </summary>
    /// <param name="adapter">The store announcing itself.</param>
    /// <param name="forAggregates">
    /// The aggregate types this store owns. A commit never spans two stores, so every aggregate on
    /// the host must be reachable from exactly one selected store: either named here, or left to the
    /// one store selected through <see cref="UsePersistence(IPersistenceAdapter)"/> or the overload
    /// without <paramref name="forAggregates"/>, which owns whatever no other store claims.
    /// </param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="adapter"/> or <paramref name="forAggregates"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The same adapter type was already selected with different arguments; an aggregate named here
    /// is already claimed by another selected store; or <see cref="UseNoPersistence"/> was already
    /// selected.
    /// </exception>
    /// <remarks>
    /// Normally reached through a store package entry point rather than called directly. Selecting
    /// two stores this way, each owning its own aggregates, is how one host runs an event-sourced
    /// aggregate and a state-stored aggregate side by side: each commit still touches exactly one
    /// store, because the two aggregate families never share a transaction.
    /// </remarks>
    public ThesseraOptions UsePersistence(IPersistenceAdapter adapter, params Type[] forAggregates)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(forAggregates);

        _persistence.Use(adapter, forAggregates);
        return this;
    }

    /// <summary>
    /// Allows an event-sourced aggregate to run on a state store, giving up its history on purpose.
    /// </summary>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// <strong>Pull this handle deliberately.</strong> The state and the version are still written
    /// correctly and the outbox is still fed, so nothing fails at run time — what is missing is the
    /// stream. The aggregate can never be replayed, audited or inspected as of an earlier point in
    /// time, and that loss is silent and permanent.
    /// <para>
    /// It is refused on an event store, where the history it waives is the one being written, and
    /// refused without a store, where there is no history to waive.
    /// </para>
    /// </remarks>
    public ThesseraOptions WithoutEventHistory()
    {
        Wiring.Persistence.WaiveEventHistory();
        return this;
    }

    /// <summary>
    /// Selects the transport integration events leave through.
    /// </summary>
    /// <param name="adapter">The transport announcing itself.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="adapter"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Without one, no integration event leaves the service: the runtime falls back to a sink that
    /// logs a warning per discarded event, while domain events and projections keep running. A
    /// transport also needs a store, because a durable endpoint needs the message store.
    /// </remarks>
    public ThesseraOptions UseMessagingTransport(IMessagingTransportAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        _messaging.UseTransport(adapter);
        return this;
    }

    /// <summary>
    /// Declares the queue this host listens on and what is bound to it.
    /// </summary>
    /// <param name="endpointName">
    /// The durable queue. It belongs to this service, so name it after the service rather than after
    /// what it listens for.
    /// </param>
    /// <param name="consumerAssembly">The assembly holding the handlers for the incoming events.</param>
    /// <param name="topicPatterns">
    /// What to bind — <c>*</c> matches one segment and <c>#</c> matches zero or more, so
    /// <c>orders.*</c> takes everything the orders context publishes.
    /// </param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpointName"/> is empty or blank, or <paramref name="topicPatterns"/> is
    /// empty or contains a blank entry. A queue with no binding receives nothing, and neither the
    /// broker nor the message engine calls that an error.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="consumerAssembly"/> or <paramref name="topicPatterns"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// A broad pattern is safe: events this service published itself are recognised by their
    /// publishing context and skipped on arrival.
    /// </remarks>
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

    /// <summary>
    /// Says whether this host may create schema, exchanges and queues on its own.
    /// </summary>
    /// <param name="provisioning">The choice.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="provisioning"/> is not one of the declared values.
    /// </exception>
    /// <remarks>
    /// A service normally leaves this at <see cref="InfrastructureProvisioning.Never"/> and lets a
    /// migration job provision, so that starting a second instance cannot change the database or the
    /// broker.
    /// </remarks>
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
