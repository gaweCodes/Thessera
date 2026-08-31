using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Application.IntegrationEvents;

/// <summary>
/// Declares the routing key an integration event is published under.
/// </summary>
/// <remarks>
/// The topic is part of the published contract: consumers bind to patterns over it, so changing the
/// value breaks everyone who has ever subscribed. It is deliberately independent of the CLR type the
/// attribute sits on, so renaming the record costs nothing.
/// <para>
/// The first segment names the owning bounded context and is checked at publish time against the
/// context name this host registered — a service cannot publish under a foreign context and
/// impersonate another one. This check is runtime-dependent; see "What this package promises" in
/// the package README.
/// </para>
/// <para>
/// Topic routing lives in the broker-neutral core, so this attribute takes effect on any transport
/// and a transport author contributes nothing to make it work.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventTopicAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEventTopicAttribute"/> class.
    /// </summary>
    /// <param name="topic">
    /// The routing key, in the form <c>&lt;context&gt;.&lt;event&gt;</c> — exactly two segments,
    /// both lower-case kebab-case, for example <c>orders.order-placed</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="topic"/> is empty or blank, does not have exactly two segments, or has a
    /// segment that is not a valid contract name.
    /// </exception>
    public IntegrationEventTopicAttribute(string topic)
    {
        Topic = Validate(topic);
    }

    /// <summary>
    /// Gets the routing key this event is published under.
    /// </summary>
    public string Topic { get; }

    private static string Validate(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var segments = topic.Split('.');
        return segments.Length == 2 && Array.TrueForAll(segments, NameSegment.IsValid)
            ? topic
            : throw new ArgumentException(
                $"'{topic}' is not a valid integration event topic. A topic is the published routing key " +
                "in the form '<context>.<event>', both segments lower-case kebab-case " +
                "(for example 'orders.order-placed'), so that consumer bindings such as 'orders.*' " +
                "stay stable and independent of the CLR type the attribute happens to be written on.",
                nameof(topic));
    }
}
