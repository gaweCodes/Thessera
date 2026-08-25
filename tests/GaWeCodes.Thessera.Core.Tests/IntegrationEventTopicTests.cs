using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

namespace GaWeCodes.Thessera.Tests;

public class TopicResolverTests
{
    [Fact]
    public void For_AnEventDeclaringItsTopic_ReturnsTheDeclaredTopic()
    {
        Assert.Equal("probe.topic-declared", TopicResolver.For(typeof(TopicDeclaredIntegrationEvent)));
    }

    [Fact]
    public void For_AnEventWithoutTheAttribute_ThrowsNamingTheEventAndTheFix()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TopicResolver.For(typeof(TopicMissingIntegrationEvent)));

        Assert.Contains(nameof(TopicMissingIntegrationEvent), exception.Message, StringComparison.Ordinal);
        Assert.Contains("IntegrationEventTopic", exception.Message, StringComparison.Ordinal);
    }

    [IntegrationEventTopic("probe.topic-declared")]
    private sealed record TopicDeclaredIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;

    private sealed record TopicMissingIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent;
}
