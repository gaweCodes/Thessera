using GaWeCodes.Thessera.Core.Messaging.IntegrationEvents;
using GaWeCodes.Thessera.Wolverine.Messaging.IntegrationEvents;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;

namespace GaWeCodes.Thessera.Tests;

public sealed class OwnContextIntegrationEventFilterTests
{
    private static readonly IntegrationEventSourceContext Consumer = new(TestMessaging.ContextName);

    [Fact]
    public void AnEventFromTheOwnContext_IsStopped()
    {
        var envelope = EnvelopeWith(TestMessaging.ContextName);

        Assert.Equal(HandlerContinuation.Stop, Filter(envelope));
    }

    [Fact]
    public void AnEventFromAForeignContext_Continues()
    {
        var envelope = EnvelopeWith(TestMessaging.UpstreamContextName);

        Assert.Equal(HandlerContinuation.Continue, Filter(envelope));
    }

    [Fact]
    public void AnEventWithoutTheSourceHeader_Continues()
    {
        Assert.Equal(HandlerContinuation.Continue, Filter(new Envelope()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEventWithABlankSourceHeader_Continues(string sourceContext)
    {
        Assert.Equal(HandlerContinuation.Continue, Filter(EnvelopeWith(sourceContext)));
    }

    [Fact]
    public void TheComparisonIsOrdinal_SoACasedLookalikeIsNotTreatedAsTheOwnContext()
    {
        var envelope = EnvelopeWith(TestMessaging.ContextName.ToUpperInvariant());

        Assert.Equal(HandlerContinuation.Continue, Filter(envelope));
    }

    private static HandlerContinuation Filter(Envelope envelope) =>
        OwnContextIntegrationEventFilter.Before(envelope, Consumer, NullLogger.Instance);

    private static Envelope EnvelopeWith(string sourceContext)
    {
        var envelope = new Envelope();
        envelope.Headers[IntegrationEventSourceContext.HeaderName] = sourceContext;
        return envelope;
    }
}
