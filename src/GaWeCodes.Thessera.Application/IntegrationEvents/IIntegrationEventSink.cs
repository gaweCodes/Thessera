namespace GaWeCodes.Thessera.Application.IntegrationEvents;

/// <summary>
/// Where a mapped integration event is handed off to leave the service.
/// </summary>
/// <remarks>
/// A transport package supplies one. Without a transport the runtime falls back to a sink that logs
/// a warning per discarded event — so a service with no transport is quiet on the wire but not
/// silent in its logs.
/// </remarks>
public interface IIntegrationEventSink
{
    /// <summary>
    /// Hands one integration event to the transport.
    /// </summary>
    /// <param name="integrationEvent">
    /// The event to publish. Its topic comes from its
    /// <see cref="IntegrationEventTopicAttribute"/> and is applied by the runtime, not by the sink.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the event has been accepted for delivery.</returns>
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
