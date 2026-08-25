using GaWeCodes.Thessera.Application.IntegrationEvents;

namespace GaWeCodes.Thessera.Tests;

public class IntegrationEventTopicAttributeTests
{
    [Theory]
    [InlineData("orders.order-placed")]
    [InlineData("sample.widget-created")]
    [InlineData("billing.invoice-payment-received-v2")]
    [InlineData("a.b")]
    public void Constructor_WithContextDotEventInKebabCase_ExposesTheTopic(string topic)
    {
        var attribute = new IntegrationEventTopicAttribute(topic);

        Assert.Equal(topic, attribute.Topic);
    }

    [Theory]
    [InlineData("order-placed")]
    [InlineData("orders.order.placed")]
    [InlineData("Orders.order-placed")]
    [InlineData("orders.Order-Placed")]
    [InlineData("orders.order--placed")]
    [InlineData("orders.-order-placed")]
    [InlineData("orders.order-placed-")]
    [InlineData("orders.")]
    [InlineData(".order-placed")]
    [InlineData("orders.order_placed")]
    [InlineData("orders.order placed")]
    public void Constructor_WithInvalidTopic_Throws(string topic)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IntegrationEventTopicAttribute(topic));

        Assert.Contains(topic, exception.Message, StringComparison.Ordinal);
        Assert.Contains("<context>.<event>", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyTopic_Throws(string topic)
    {
        Assert.Throws<ArgumentException>(() => new IntegrationEventTopicAttribute(topic));
    }

    [Fact]
    public void Constructor_WithNullTopic_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IntegrationEventTopicAttribute(null!));
    }
}
