using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

public sealed class DomainEventTypeRegistry
{
    private readonly Dictionary<string, Type> _typesByName = [];
    private readonly Dictionary<Type, string> _namesByType = [];

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

    public IReadOnlyDictionary<Type, string> NamesByType => _namesByType;

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
