using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Wolverine.Messaging.Transport;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.DependencyInjection.Wiring;

internal sealed class ThesseraWolverineExtension(IWiringSnapshot wiring) : IWolverineExtension
{
    public void Configure(WolverineOptions options)
    {
        options.UseSystemTextJsonForSerialization(EntityKeyJsonOptions.Apply);

        if (wiring.PersistenceSelected)
        {
            options.ApplyThesseraIdempotencyWindow();
            options.ApplyThesseraMessageStorageProvisioning(wiring.ProvisionsInfrastructure);
            options.ApplyThesseraDomainEventRouting();
        }

        options.ApplyThesseraMessagingPolicies(wiring.IsTransientFault);

        if (wiring.Transport is { } transport)
        {
            if (transport is not IWolverineMessagingTransport wolverineTransport)
            {
                throw new InvalidOperationException(
                    $"The messaging transport {transport.Description} does not implement " +
                    $"{nameof(IWolverineMessagingTransport)}, so the Wolverine runtime cannot configure it. The " +
                    "host would start with a transport that is selected but never wired, and every integration " +
                    "event would be dropped silently. Implement the interface on the transport adapter or select " +
                    "a transport that targets this runtime.");
            }

            options.ApplyThesseraIntegrationEventTopics(wolverineTransport.ContextName);
            wolverineTransport.Configure(options, wiring.ProvisionsInfrastructure);

            if (wiring.Subscription is { } subscription)
            {
                options.ApplyThesseraSubscriptionDiscovery(subscription);
                wolverineTransport.ConfigureSubscription(options, subscription);
            }
        }
    }
}
