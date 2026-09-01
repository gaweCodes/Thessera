using System.Diagnostics.CodeAnalysis;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain.Entities;

namespace GaWeCodes.Thessera.Testing;

/// <summary>
/// Builds the <see cref="DomainEventMetadata"/> that the runtime would hand a projection handler,
/// so a handler can be tested by calling it directly.
/// </summary>
/// <remarks>
/// The aggregate name and the key text come from <see cref="EntityKeyFormatter"/>, the same code
/// the runtime uses. A hand-written stub calling <c>ToString()</c> on the key agrees with it today
/// and stops agreeing the moment the key type changes.
/// </remarks>
public static class TestMetadata
{
    /// <summary>
    /// Builds the metadata the runtime would hand a projection handler for one event of
    /// <typeparamref name="TAggregate"/>.
    /// </summary>
    /// <typeparam name="TAggregate">
    /// The aggregate the event belongs to. Its <c>[AggregateName]</c> supplies the name in the
    /// metadata.
    /// </typeparam>
    /// <param name="aggregateId">The aggregate's identity. Its <c>[AggregateName]</c> supplies the name.</param>
    /// <param name="version">The version the event carries. Projections use it as their watermark.</param>
    /// <param name="eventId">Defaults to a new value; pass one to test redelivery of the same event.</param>
    /// <param name="occurredAt">Defaults to <see cref="DateTimeOffset.UnixEpoch"/> so tests stay deterministic.</param>
    /// <returns>Metadata identical in shape to what the runtime produces.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aggregateId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="version"/> is zero or negative. A version counts applied events, so the first
    /// event a handler can see carries 1.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TAggregate"/> has no <c>[AggregateName]</c>, or
    /// <paramref name="aggregateId"/> is empty or of a key type that has no declared stream-key
    /// format.
    /// </exception>
    [RequiresUnreferencedCode(TrimmingMessages.TypedKeyReflection)]
    [RequiresDynamicCode(TrimmingMessages.TypedKeyReflection)]
    public static DomainEventMetadata For<TAggregate>(
        IEntityKey aggregateId,
        long version,
        Guid? eventId = null,
        DateTimeOffset? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(aggregateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        return new DomainEventMetadata(
            eventId ?? Guid.NewGuid(),
            EntityKeyFormatter.GetAggregateName(typeof(TAggregate)),
            EntityKeyFormatter.GetKeyValue(aggregateId),
            version,
            occurredAt ?? DateTimeOffset.UnixEpoch);
    }
}
