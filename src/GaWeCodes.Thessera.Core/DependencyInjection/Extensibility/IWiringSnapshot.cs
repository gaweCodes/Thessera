using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Messaging.Transport;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;

public interface IWiringSnapshot
{
    bool RequiresRuntime { get; }

    bool PersistenceSelected { get; }

    bool ProvisionsInfrastructure { get; }

    IMessagingTransportAdapter? Transport { get; }

    IntegrationEventSubscription? Subscription { get; }

    bool IsTransientFault(Exception exception);
}
