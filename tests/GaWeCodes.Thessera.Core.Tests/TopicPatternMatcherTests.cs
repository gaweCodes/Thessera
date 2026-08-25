using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;

namespace GaWeCodes.Thessera.Tests;

public sealed class TopicPatternMatcherTests
{
    [Theory]
    [InlineData("orders.*", "orders.order-placed")]
    [InlineData("*.order-placed", "orders.order-placed")]
    [InlineData("orders.order-placed", "orders.order-placed")]
    [InlineData("#", "orders.order-placed")]
    [InlineData("orders.#", "orders.order-placed")]
    [InlineData("orders.#", "orders.order.placed.v2")]
    [InlineData("orders.#", "orders")]
    [InlineData("#.placed", "orders.order.placed")]
    public void MatchingPattern_IsRecognised(string pattern, string topic) =>
        Assert.True(TopicPatternMatcher.Matches(pattern, topic));

    [Theory]
    [InlineData("orders.*", "billing.order-placed")]
    [InlineData("orders.*", "orders.order.placed")]
    [InlineData("orders.*", "orders")]
    [InlineData("order.*", "orders.order-placed")]
    [InlineData("orders.order-placed", "orders.order-placedx")]
    [InlineData("orders.#", "billing.order-placed")]
    public void NonMatchingPattern_IsRejected(string pattern, string topic) =>
        Assert.False(TopicPatternMatcher.Matches(pattern, topic));

    [Fact]
    public void SingleWildcard_MatchesExactlyOneWord()
    {
        Assert.True(TopicPatternMatcher.Matches("*.*", "a.b"));
        Assert.False(TopicPatternMatcher.Matches("*.*", "a.b.c"));
        Assert.False(TopicPatternMatcher.Matches("*.*", "a"));
    }
}
