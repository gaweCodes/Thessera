namespace GaWeCodes.Thessera.Core.Messaging.Transport;

public interface IMessageEmitter
{
    Task PublishAsync(object message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken);
}
