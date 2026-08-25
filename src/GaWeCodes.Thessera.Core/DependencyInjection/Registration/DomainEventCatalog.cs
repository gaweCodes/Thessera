using System.Reflection;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Registration;

internal sealed class DomainEventCatalog
{
    private readonly HashSet<Assembly> _assemblies = [];
    private DomainEventTypeRegistry? _registry;

    public IReadOnlyCollection<Assembly> Assemblies => _assemblies;

    public DomainEventTypeRegistry Registry => _registry ??= new DomainEventTypeRegistry(_assemblies);

    public void Add(Assembly assembly)
    {
        if (_registry is not null)
        {
            throw new InvalidOperationException(
                "AddDomainEventsFrom was called after the domain event names had already been read. " +
                "Register every domain event assembly inside the AddThessera callback.");
        }

        _assemblies.Add(assembly);
    }
}
