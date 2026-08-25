using GaWeCodes.Thessera.Core.Messaging.Transport;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

internal sealed class MessagingSelection
{
    public IMessagingTransportAdapter? Transport { get; private set; }

    public IntegrationEventSubscription? Subscription { get; private set; }

    public bool IsSelected => Transport is not null;

    public void SelectTransport(IMessagingTransportAdapter transport) => Transport = transport;

    public void SelectSubscription(IntegrationEventSubscription subscription)
    {
        if (Subscription is not null)
        {
            throw new InvalidOperationException(
                "SubscribeToIntegrationEvents was called more than once. A microservice hosts exactly one bounded " +
                "context and owns exactly one queue; bind every topic pattern it consumes to that one " +
                "queue in a single call instead.");
        }

        Subscription = subscription;
    }
}
