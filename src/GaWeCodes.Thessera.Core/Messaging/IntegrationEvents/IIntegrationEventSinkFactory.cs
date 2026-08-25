using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.Transport;

namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

public interface IIntegrationEventSinkFactory
{
    IIntegrationEventSink Create(IMessageEmitter emitter);
}
