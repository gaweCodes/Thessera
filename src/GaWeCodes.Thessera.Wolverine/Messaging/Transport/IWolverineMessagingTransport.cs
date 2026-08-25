using GaWeCodes.Thessera.Core.DependencyInjection.Wiring;
using GaWeCodes.Thessera.Core.Messaging.Transport;
using Wolverine;

namespace GaWeCodes.Thessera.Wolverine.Messaging.Transport;

public interface IWolverineMessagingTransport : IMessagingTransportAdapter
{
    void Configure(WolverineOptions options, bool provisionInfrastructure);

    void ConfigureSubscription(WolverineOptions options, IntegrationEventSubscription subscription);
}
