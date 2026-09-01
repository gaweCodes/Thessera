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

    /// <summary>
    /// Builds the catalogue by scanning the given assemblies for domain events.
    /// </summary>
    /// <param name="assemblies">The assemblies declaring the domain events.</param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// An assembly could not be loaded, a domain event has no persisted name, a type claims two
    /// names, or two types claim the same name — a persisted name identifies exactly one type.
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
    }

    /// <summary>
    /// Gets every known domain event type and the name it is written under.
    /// </summary>
    /// <remarks>
    /// A store uses this to register the same names with its own event mapping, so that both sides
    /// agree on what a stored event is called.
    /// </remarks>
    public IReadOnlyDictionary<Type, string> NamesByType => _namesByType;

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
}
