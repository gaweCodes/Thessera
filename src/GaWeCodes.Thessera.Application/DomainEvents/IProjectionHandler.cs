using GaWeCodes.Thessera.Domain.Events;

namespace GaWeCodes.Thessera.Application.DomainEvents;

/// <summary>
/// Reacts to one domain event in order to write a read model.
/// </summary>
/// <typeparam name="TDomainEvent">The event this handler is interested in.</typeparam>
/// <remarks>
/// Projections run after the transaction has committed, so a slow one cannot hold up the command
/// that raised the event. Several handlers may take the same event, and one handler may take
/// several events by implementing this interface more than once. Running on a durable queue is a
/// runtime-dependent guarantee; see "What this package promises" in the package README.
/// <para>
/// A read model is derived and can always be rebuilt, which is what makes it safe to throw away
/// when a projection changes.
/// </para>
/// </remarks>
public interface IProjectionHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>
    /// Applies the event to the read model.
    /// </summary>
    /// <param name="domainEvent">The event that happened.</param>
    /// <param name="metadata">
    /// Its context. Compare <see cref="DomainEventMetadata.Version"/> against what the read model
    /// already holds and ignore anything not newer.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the read model has been updated.</returns>
    /// <remarks>
    /// Write idempotently. Delivery is at-least-once, so this method will occasionally be called
    /// twice with the same event, and a handler that blindly increments will drift.
    /// </remarks>
    Task HandleAsync(TDomainEvent domainEvent, DomainEventMetadata metadata, CancellationToken cancellationToken);
}
