using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Tests;

public sealed class ContractNameTests
{
    [Theory]
    [InlineData("widget")]
    [InlineData("widget-created")]
    [InlineData("widget-created-v1")]
    [InlineData("v2")]
    [InlineData("a1-b2-c3")]
    public void KebabCaseNames_AreAccepted(string name)
    {
        Assert.Equal(name, new EventNameAttribute(name).Name);
        Assert.Equal(name, new AggregateNameAttribute(name).Name);
    }

    [Theory]
    [InlineData("WidgetCreated")]
    [InlineData("widget_created")]
    [InlineData("widget.created")]
    [InlineData("widget created")]
    [InlineData("-widget")]
    [InlineData("widget-")]
    [InlineData("widget--created")]
    [InlineData("Widget")]
    public void NamesThatAreNotKebabCase_AreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new EventNameAttribute(name));
        Assert.Throws<ArgumentException>(() => new AggregateNameAttribute(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNames_AreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new EventNameAttribute(name));
        Assert.Throws<ArgumentException>(() => new AggregateNameAttribute(name));
    }

    [Fact]
    public void NullName_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EventNameAttribute(null!));
        Assert.Throws<ArgumentNullException>(() => new AggregateNameAttribute(null!));
    }
}
