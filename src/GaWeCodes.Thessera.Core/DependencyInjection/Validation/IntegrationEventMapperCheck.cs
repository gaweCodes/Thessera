using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Core.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Validation;

internal sealed class IntegrationEventMapperCheck(
    IServiceProvider serviceProvider,
    DomainEventTypeRegistry domainEventTypes) : SynchronousStartupCheck
{
    public override StartupPhase Phase => StartupPhase.BeforeHostedServicesStart;

    protected override void Run()
    {
        if (serviceProvider.GetService<IIntegrationEventSinkFactory>() is not NullIntegrationEventSinkFactory)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var mappers = domainEventTypes.NamesByType.Keys
            .SelectMany(domainEventType => scope.ServiceProvider.GetServices(
                typeof(IIntegrationEventMapper<>).MakeGenericType(domainEventType)))
            .Select(mapper => $"'{mapper!.GetType()}'")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (mappers.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Integration-event mappers are registered, but no messaging transport is configured: " +
            $"{string.Join(", ", mappers)}. A mapper exists for one purpose — turning a domain event into an " +
            "integration event that leaves this context — so every event it produces would be handed to the null " +
            "sink and dropped after a log warning, while the commit reports success and every downstream context " +
            "silently stops receiving. Select a messaging transport, " +
            "or delete the mapper if this context publishes nothing.");
    }
}
