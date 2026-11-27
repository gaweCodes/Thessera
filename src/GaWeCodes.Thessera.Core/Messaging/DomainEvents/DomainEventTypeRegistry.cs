using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

/// <summary>
/// The catalogue of persisted event names: which name a domain event type is written under, and
/// which type a stored name resolves back to.
/// </summary>
/// <remarks>
/// Built from the assemblies handed to <c>AddDomainEventsFrom</c>, and built eagerly — an event
/// without a name, or two events claiming the same one, is refused while the host is composed rather
/// than at the first write.
/// </remarks>
public sealed class DomainEventTypeRegistry
{
    private readonly Dictionary<string, Type> _typesByName = [];
    private readonly Dictionary<Type, string> _namesByType = [];
    private readonly Dictionary<Type, Type> _successors = [];

    /// <summary>
    /// Builds the catalogue by scanning the given assemblies for domain events.
    /// </summary>
    /// <param name="assemblies">The assemblies declaring the domain events.</param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// An assembly could not be loaded, a domain event has no persisted name, a type claims two
    /// names, or two types claim the same name — a persisted name identifies exactly one type. Also
    /// thrown when a retired event names more than one successor, a successor that is not
    /// registered, or a chain of successors that leads back to itself.
    /// </exception>
    [RequiresUnreferencedCode(TrimmingMessages.AssemblyScanning)]
    public DomainEventTypeRegistry(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            foreach (var domainEventType in DomainEventTypesIn(assembly))
            {
                Register(domainEventType);
            }
        }

        RegisterSuccessors();
        RefuseCycles();

        NamesByType = _namesByType.AsReadOnly();
    }

    /// <summary>
    /// Gets every known domain event type and the name it is written under.
    /// </summary>
    /// <remarks>
    /// A store uses this to register the same names with its own event mapping, so that both sides
    /// agree on what a stored event is called.
    /// </remarks>
    public IReadOnlyDictionary<Type, string> NamesByType { get; }

    /// <summary>
    /// Looks up the persisted name a domain event type is written under.
    /// </summary>
    /// <param name="domainEventType">The event type.</param>
    /// <returns>Its persisted name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domainEventType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The type is not registered, because the assembly declaring it was not passed to
    /// <c>AddDomainEventsFrom</c>. The name has to be known before the first event is written.
    /// </exception>
    public string NameOf(Type domainEventType)
    {
        ArgumentNullException.ThrowIfNull(domainEventType);

        return _namesByType.TryGetValue(domainEventType, out var name)
            ? name
            : throw new InvalidOperationException(
                $"The domain event type '{domainEventType}' is not registered. Pass the assembly that declares it " +
                "to ThesseraOptions.AddDomainEventsFrom, so that its persisted name is known before the " +
                "first event is written.");
    }

    /// <summary>
    /// Looks up the type a stored event name belongs to.
    /// </summary>
    /// <param name="eventName">The persisted name read from the store or the wire.</param>
    /// <returns>The type to deserialize the payload as.</returns>
    /// <exception cref="ArgumentException"><paramref name="eventName"/> is empty or blank.</exception>
    /// <exception cref="InvalidOperationException">
    /// Nothing is registered under that name, so a stored event cannot be read. When an event has to
    /// change, keep the retired type and its name alongside the successor rather than renaming it —
    /// both versions then stay readable.
    /// </exception>
    public Type Resolve(string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        return _typesByName.TryGetValue(eventName, out var domainEventType)
            ? domainEventType
            : throw new InvalidOperationException(
                $"No domain event type is registered under the name '{eventName}'. A stored event whose name is no " +
                "longer known cannot be read. Keep the retired type and its [EventName] alongside the successor " +
                "instead of renaming it, so that both versions stay readable.");
    }

    /// <summary>
    /// Turns a retired event into the current version of it, following the chain of successors.
    /// </summary>
    /// <param name="domainEvent">The event as it was read.</param>
    /// <returns>
    /// The event itself when it is not an <see cref="IRetiredDomainEvent{TSuccessor}"/>, otherwise
    /// the last successor in its chain.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A retired event in the chain is not registered, or its <c>Upcast</c> returned
    /// <see langword="null"/> or an instance of a type other than its declared successor.
    /// </exception>
    public IDomainEvent Upcast(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var current = domainEvent;

        while (current is IRetiredDomainEvent<IDomainEvent> retired)
        {
            var retiredType = current.GetType();

            if (!_successors.TryGetValue(retiredType, out var successorType))
            {
                throw new InvalidOperationException(
                    $"The retired domain event '{retiredType}' is not registered. Pass the assembly that declares it " +
                    "to ThesseraOptions.AddDomainEventsFrom, so that its successor is checked before it is read.");
            }

            var next = retired.Upcast();

            if (next is null || next.GetType() != successorType)
            {
                throw new InvalidOperationException(
                    $"'{retiredType}.Upcast' returned '{next?.GetType().ToString() ?? "null"}' instead of an instance of " +
                    $"its declared successor '{successorType}'.");
            }

            current = next;
        }

        return current;
    }

    private static IEnumerable<Type> DomainEventTypesIn(Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            throw new InvalidOperationException(
                $"The types of assembly '{assembly.FullName}' could not be loaded. " +
                "The most common cause is a missing package reference.",
                exception);
        }

        return types.Where(static type =>
            type is { IsClass: true, IsAbstract: false }
            && !type.IsGenericTypeDefinition
            && typeof(IDomainEvent).IsAssignableFrom(type));
    }

    private void Register(Type domainEventType)
    {
        var name = domainEventType.GetCustomAttribute<EventNameAttribute>(inherit: false)?.Name
            ?? throw new InvalidOperationException(
                $"The domain event '{domainEventType}' has no [EventName]. The name is written into every outbox " +
                "row and every event stream, so it is a persistence contract and must be chosen deliberately " +
                "instead of following the CLR type name.");

        if (_namesByType.TryGetValue(domainEventType, out var existingName))
        {
            if (existingName == name)
            {
                return;
            }

            throw new InvalidOperationException(
                $"The domain event '{domainEventType}' is registered under two names, '{existingName}' and '{name}'.");
        }

        if (_typesByName.TryGetValue(name, out var existingType))
        {
            throw new InvalidOperationException(
                $"The event name '{name}' is used by both '{existingType}' and '{domainEventType}'. " +
                "A persisted event name identifies exactly one type.");
        }

        _namesByType.Add(domainEventType, name);
        _typesByName.Add(name, domainEventType);
    }

    private void RegisterSuccessors()
    {
        foreach (var domainEventType in _namesByType.Keys)
        {
            var successors = domainEventType.GetInterfaces()
                .Where(static contract =>
                    contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRetiredDomainEvent<>))
                .Select(static contract => contract.GetGenericArguments()[0])
                .ToArray();

            if (successors.Length == 0)
            {
                continue;
            }

            if (successors.Length > 1)
            {
                throw new InvalidOperationException(
                    $"The retired domain event '{domainEventType}' names more than one successor: " +
                    $"{string.Join(", ", successors.Select(static successor => $"'{successor}'"))}. " +
                    "A stored event is read as exactly one successor.");
            }

            var successor = successors[0];

            if (!_namesByType.ContainsKey(successor))
            {
                throw new InvalidOperationException(
                    $"The successor '{successor}' of the retired domain event '{domainEventType}' is not a registered " +
                    "domain event. It has to be a concrete type in an assembly passed to " +
                    "ThesseraOptions.AddDomainEventsFrom.");
            }

            _successors.Add(domainEventType, successor);
        }
    }

    private void RefuseCycles()
    {
        foreach (var start in _successors.Keys)
        {
            var visited = new HashSet<Type> { start };
            var current = start;

            while (_successors.TryGetValue(current, out var next))
            {
                if (!visited.Add(next))
                {
                    throw new InvalidOperationException(
                        $"The successors of the retired domain event '{start}' lead back to '{next}'. " +
                        "A chain of successors has to end at an event that is not retired.");
                }

                current = next;
            }
        }
    }
}
