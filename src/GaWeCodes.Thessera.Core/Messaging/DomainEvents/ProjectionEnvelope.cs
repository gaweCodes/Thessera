namespace GaWeCodes.Thessera.Core.Messaging.DomainEvents;

/// <summary>
/// The same domain event again, on its way to the projection queue.
/// </summary>
/// <param name="Event">The domain event envelope to project.</param>
/// <remarks>
/// A distinct type for what is otherwise the same content, so that a slow projection cannot hold up
/// delivery of integration events, and a dead-lettered projection does not take the publishing half
/// down with it. It is queued after the mappers have run, and partitioned by the same aggregate
/// key. Whether that queue is durable is runtime-dependent; see "What this package promises" in the
/// package README.
/// </remarks>
public sealed record ProjectionEnvelope(DomainEventEnvelope Event);
