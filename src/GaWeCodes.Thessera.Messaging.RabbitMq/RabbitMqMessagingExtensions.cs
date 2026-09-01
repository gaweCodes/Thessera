using GaWeCodes.Thessera.Core.DependencyInjection;
using GaWeCodes.Thessera.Messaging.RabbitMq;

// Deliberate exception to the package/namespace rule. The composition entry points stay in the
// shared root namespace so a consumer's Program.cs reaches AddThessera and every Use*
// call with one using -- the same reason AddConsole() lives in Microsoft.Extensions.Logging
// and not in Microsoft.Extensions.Logging.Console. Every other type in this package matches
// its package name, so IDE0130 is suppressed here and nowhere else.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace GaWeCodes.Thessera;
#pragma warning restore IDE0130
/// <summary>
/// The entry point that lets a service publish its integration events to RabbitMQ and subscribe to
/// other services' events.
/// </summary>
public static class RabbitMqMessagingExtensions
{
    /// <summary>
    /// Selects RabbitMQ as this host's transport for integration events.
    /// </summary>
    /// <param name="options">The options being configured inside <c>AddThessera</c>.</param>
    /// <param name="rabbitMqUri">The broker connection.</param>
    /// <param name="exchangeName">
    /// The shared durable topic exchange. Every participating service names the same one.
    /// </param>
    /// <param name="contextName">
    /// This service's bounded context: a single lower-case kebab-case word without a dot. It is the
    /// first segment of every routing key the host publishes under, and publishing an event whose
    /// topic names a different context is refused rather than allowed to impersonate that service.
    /// </param>
    /// <returns>The same <paramref name="options"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="rabbitMqUri"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="exchangeName"/> or <paramref name="contextName"/> is empty or blank, or
    /// <paramref name="contextName"/> is not a valid contract name.
    /// </exception>
    /// <remarks>
    /// Without a transport nothing leaves the service: the runtime falls back to a sink that logs a
    /// warning per discarded integration event, while domain events and projections keep running.
    /// <para>
    /// The exchange and queues are declared but not created unless the host also says
    /// <c>ProvisionInfrastructure(InfrastructureProvisioning.AtStartup)</c>. A startup check
    /// otherwise verifies that the topology is actually there, rather than letting the host start
    /// against a broker that would discard everything it publishes.
    /// </para>
    /// </remarks>
    public static ThesseraOptions UseWolverineMessaging(
        this ThesseraOptions options,
        Uri rabbitMqUri,
        string exchangeName,
        string contextName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(rabbitMqUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);

        return options.UseMessagingTransport(new RabbitMqTransportAdapter(rabbitMqUri, exchangeName, contextName));
    }
}
