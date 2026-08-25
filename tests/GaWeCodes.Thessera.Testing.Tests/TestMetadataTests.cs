using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Testing;
using HullFixture;

namespace GaWeCodes.Thessera.Tests;

public sealed class TestMetadataTests
{
    private static readonly SealedHullId Id = new(Guid.Parse("2f1c6a7e-9b48-4d3a-8f21-0c5e7d9a4b16"));

    [Fact]
    public void TheAggregateName_ComesFromTheAttribute_NotTheTypeName() =>
        Assert.Equal("sealed-hull", TestMetadata.For<SealedHull>(Id, 1).AggregateName);

    // The point of the helper: a hand-written stub calling ToString() on the key agrees with the
    // runtime today and stops agreeing the moment the key's value type changes.
    [Fact]
    public void TheAggregateId_IsFormattedTheWayTheRuntimeFormatsIt() =>
        Assert.Equal(
            EntityKeyFormatter.GetKeyValue(Id),
            TestMetadata.For<SealedHull>(Id, 1).AggregateId);

    [Fact]
    public void TheTimestamp_IsDeterministicUnlessGiven() =>
        Assert.Equal(DateTimeOffset.UnixEpoch, TestMetadata.For<SealedHull>(Id, 1).OccurredAt);

    [Fact]
    public void TheEventId_CanBePinned_SoRedeliveryOfTheSameEventIsTestable()
    {
        var eventId = Guid.NewGuid();

        Assert.Equal(eventId, TestMetadata.For<SealedHull>(Id, 1, eventId).EventId);
        Assert.NotEqual(
            TestMetadata.For<SealedHull>(Id, 1).EventId,
            TestMetadata.For<SealedHull>(Id, 1).EventId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveVersion_Throws(long version) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TestMetadata.For<SealedHull>(Id, version));

    [Fact]
    public void AnEmptyKey_Throws() =>
        Assert.Throws<InvalidOperationException>(
            () => TestMetadata.For<SealedHull>(default(SealedHullId), 1));
}
