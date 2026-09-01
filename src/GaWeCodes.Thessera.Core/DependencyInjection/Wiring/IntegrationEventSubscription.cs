using System.Reflection;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Wiring;

/// <summary>
/// What a host declared in order to receive other services' integration events.
/// </summary>
/// <param name="EndpointName">
/// The durable queue to listen on. It belongs to this service, so name it after the service rather
/// than after what it listens for.
/// </param>
/// <param name="TopicPatterns">
/// The patterns bound to that queue — <c>*</c> matches one segment and <c>#</c> matches zero or
/// more, so <c>orders.*</c> takes everything the orders context publishes. At least one non-blank
/// pattern is required: a queue with no binding receives nothing, and neither the broker nor the
/// message engine calls that an error.
/// </param>
/// <param name="ConsumerAssembly">The assembly the consuming handlers are found in.</param>
/// <remarks>
/// A host declares at most one subscription. Events this service published itself are skipped on
/// arrival by their publishing context, so a broad pattern does not make a service consume its own
/// echo.
/// </remarks>
public sealed record IntegrationEventSubscription(
    string EndpointName,
    IReadOnlyList<string> TopicPatterns,
    Assembly ConsumerAssembly);
