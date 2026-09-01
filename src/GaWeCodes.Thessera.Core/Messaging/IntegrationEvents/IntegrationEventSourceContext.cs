namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

/// <summary>
/// The publishing context of this host, stamped onto every integration event it sends.
/// </summary>
/// <param name="name">
/// The bounded context this host publishes as — the same name that must be the first segment of
/// every topic it may publish under.
/// </param>
/// <remarks>
/// This is how a service recognises and skips the events it published itself, so it can bind a broad
/// pattern without consuming its own echo. Do not confuse it with the context segment of a topic:
/// the two are the same string for a service's own events, but this one is a property of the
/// message, not of the type.
/// </remarks>
public sealed class IntegrationEventSourceContext(string name)
{
    /// <summary>
    /// The message header the publishing context travels in.
    /// </summary>
    /// <remarks>
    /// Part of the wire format: a consumer written against another stack can read it, and a service
    /// that stops sending it starts consuming its own events again.
    /// </remarks>
    public const string HeaderName = "thessera.source-context";

    /// <summary>
    /// Gets the name of the bounded context this host publishes as.
    /// </summary>
    public string Name { get; } = name;
}
