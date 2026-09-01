using System.Collections.Concurrent;
using System.Reflection;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

/// <summary>
/// Reads the routing key an integration event is published under, and refuses to let a service
/// publish under a context that is not its own.
/// </summary>
/// <remarks>
/// Broker-neutral on purpose: topic routing lives here rather than in a transport package, so
/// <c>[IntegrationEventTopic]</c> takes effect on any transport and a transport author contributes
/// nothing to make it work.
/// </remarks>
public static class TopicResolver
{
    private static readonly ConcurrentDictionary<Type, string> Topics = new();

    /// <summary>
    /// Reads the topic and checks that this host is allowed to publish it.
    /// </summary>
    /// <param name="integrationEventType">The event type being published.</param>
    /// <param name="contextName">The bounded context this host publishes as.</param>
    /// <returns>The routing key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="integrationEventType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="contextName"/> is empty or blank.</exception>
    /// <exception cref="InvalidOperationException">
    /// The event declares a topic whose context segment is not this host — publishing it would make
    /// this service impersonate another one, and consumers bind to that segment as the owner of the
    /// contract. Publish it from the owning context, or correct the attribute.
    /// </exception>
    public static string For(Type integrationEventType, string contextName)
    {
        ArgumentNullException.ThrowIfNull(integrationEventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);

        var topic = For(integrationEventType);

        return ContextOf(topic).Equals(contextName, StringComparison.Ordinal)
            ? topic
            : throw new InvalidOperationException(
                $"The integration event '{integrationEventType.FullName}' declares the topic '{topic}', but this " +
                $"host is the bounded context '{contextName}'. The context segment of a routing key names the " +
                "owner of the contract, and consumers bind to it; publishing under a foreign context makes this " +
                "service impersonate another one. Publish it from the owning context, or correct the " +
                "[IntegrationEventTopic] attribute.");
    }

    /// <summary>
    /// Reads the owning context out of a topic.
    /// </summary>
    /// <param name="topic">The routing key.</param>
    /// <returns>
    /// The segment before the first dot — the whole string when there is no dot.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="topic"/> is empty or blank.</exception>
    public static string ContextOf(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var separator = topic.IndexOf('.', StringComparison.Ordinal);
        return separator < 0 ? topic : topic[..separator];
    }

    /// <summary>
    /// Reads the topic an integration event declares, without checking who may publish it.
    /// </summary>
    /// <param name="integrationEventType">The event type.</param>
    /// <returns>The routing key from its <c>[IntegrationEventTopic]</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="integrationEventType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The type carries no <c>[IntegrationEventTopic]</c>. Without one the event would be published
    /// under a key nobody has bound and disappear silently.
    /// </exception>
    public static string For(Type integrationEventType)
    {
        ArgumentNullException.ThrowIfNull(integrationEventType);

        return Topics.GetOrAdd(integrationEventType, static type =>
            type.GetCustomAttribute<IntegrationEventTopicAttribute>(inherit: false)?.Topic
            ?? throw new InvalidOperationException(
                $"The integration event '{type.FullName}' carries no [IntegrationEventTopic] attribute. " +
                "The topic is the routing key this event is published under and part of the published contract; " +
                "without it the event would be published under a key no consumer has bound and silently " +
                "disappear. Declare it as [IntegrationEventTopic(\"<context>.<event>\")]."));
    }
}
