namespace GaWeCodes.Thessera.Core.Messaging.Transport;

/// <summary>
/// The narrowest view of a message engine: publish this object, with these headers.
/// </summary>
/// <remarks>
/// It exists so that the core can hand something to a sink without naming a message engine. The
/// runtime supplies an implementation bound to the session currently handling a message, which is
/// what keeps a published integration event inside the same delivery transaction.
/// </remarks>
public interface IMessageEmitter
{
    /// <summary>
    /// Publishes one message.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="headers">
    /// Headers to attach, or <see langword="null"/> for none. The publishing context is added by the
    /// runtime rather than here.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the message has been accepted for delivery.</returns>
    Task PublishAsync(object message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken);
}
