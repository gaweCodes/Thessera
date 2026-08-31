namespace GaWeCodes.Thessera.Application.IntegrationEvents;

/// <summary>
/// What one service publishes for other services to consume.
/// </summary>
/// <remarks>
/// The difference from a domain event is audience, not shape. A domain event is an internal fact
/// and never leaves; an integration event is a published contract you are not free to change once
/// somebody has subscribed to it — so keep it deliberately small and independent of the domain
/// types behind it.
/// <para>
/// Every implementation needs an <see cref="IntegrationEventTopicAttribute"/>: without one there is
/// no routing key to publish under, and the event would vanish silently.
/// </para>
/// </remarks>
/// <seealso cref="IIntegrationEventMapper{TDomainEvent}"/>
public interface IIntegrationEvent
{
    /// <summary>
    /// Gets the identity of this event, so a consumer can recognise a redelivery.
    /// </summary>
    /// <value>
    /// Usually the identity of the domain event it was mapped from, which keeps the two traceable
    /// to one another.
    /// </value>
    Guid EventId { get; }

    /// <summary>
    /// Gets the instant the underlying fact happened — not the instant it was published.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
