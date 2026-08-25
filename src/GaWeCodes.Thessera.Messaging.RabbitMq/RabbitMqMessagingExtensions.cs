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
public static class RabbitMqMessagingExtensions
{
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
