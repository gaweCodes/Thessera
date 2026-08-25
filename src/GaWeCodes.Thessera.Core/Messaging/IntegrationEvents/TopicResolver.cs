using System.Collections.Concurrent;
using System.Reflection;
using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

public static class TopicResolver
{
    private static readonly ConcurrentDictionary<Type, string> Topics = new();

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

    public static string ContextOf(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var separator = topic.IndexOf('.', StringComparison.Ordinal);
        return separator < 0 ? topic : topic[..separator];
    }

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
