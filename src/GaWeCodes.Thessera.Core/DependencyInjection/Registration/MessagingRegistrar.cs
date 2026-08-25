using System.Reflection;
using GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;
using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using GaWeCodes.Thessera.Domain.Naming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Registration;

internal sealed class MessagingRegistrar(
    IServiceCollection services,
    MessagingSelection messaging,
    ProvisioningSelection provisioning,
    RuntimeActivation runtime)
{
    public void UseTransport(IMessagingTransportAdapter adapter)
    {
        var contextName = adapter.ContextName;

        if (!NameSegment.IsValid(contextName))
        {
            throw new ArgumentException(
                $"'{contextName}' is not a valid bounded-context name. It is the first segment of every routing " +
                "key this service publishes, so it must be a single lower-case kebab-case word without a dot " +
                "(for example \"orders\"). A value containing a dot is almost always the broker destination name " +
                "passed in the wrong position.",
                nameof(adapter));
        }

        services.Replace(ServiceDescriptor.Singleton<IIntegrationEventSinkFactory>(
            new IntegrationEventSinkFactory(contextName)));
        services.Replace(ServiceDescriptor.Singleton(new IntegrationEventSourceContext(contextName)));
        messaging.SelectTransport(adapter);

        adapter.Register(new MessagingTransportRegistrationContext(
            services,
            () => provisioning.ProvisionsInfrastructure,
            () => messaging.Subscription,
            runtime));
    }

    public void Subscribe(string endpointName, Assembly consumerAssembly, string[] topicPatterns)
    {
        if (topicPatterns.Length == 0 || Array.Exists(topicPatterns, string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-blank topic pattern is required. An endpoint with no binding receives nothing, " +
                "and neither the broker nor Wolverine reports that as an error.",
                nameof(topicPatterns));
        }

        messaging.SelectSubscription(new IntegrationEventSubscription(
            endpointName,
            [.. topicPatterns],
            consumerAssembly));
    }
}
