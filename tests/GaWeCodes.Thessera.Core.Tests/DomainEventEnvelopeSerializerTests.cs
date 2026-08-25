using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Tests;

public sealed class DomainEventEnvelopeSerializerTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly DateTimeOffset OccurredAt = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private static readonly DomainEventEnvelopeSerializer Serializer =
        new(new DomainEventTypeRegistry([typeof(DomainEventEnvelopeSerializerTests).Assembly]));

    [Fact]
    public void WrapThenUnwrap_WithTypedIdDecimalAndDateTimeOffset_RoundTripsAllValues()
    {
        var original = new RecipeRenamed(
            new RecipeId(Guid.NewGuid()),
            NewName: "Pasta",
            Rating: 4.75m,
            RenamedAt: new DateTimeOffset(2026, 7, 30, 9, 30, 0, TimeSpan.FromHours(2)));

        var restored = (RecipeRenamed)Serializer.Unwrap(Wrap(original));

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Wrap_CarriesTheDeclaredEventName_NotTheClrTypeName()
    {
        var envelope = Wrap(new RecipeRenamed(new RecipeId(Guid.NewGuid()), "Pizza", 5m, DateTimeOffset.UnixEpoch));

        Assert.Equal("recipe-renamed-v1", envelope.EventName);
        Assert.DoesNotContain(nameof(RecipeRenamed), envelope.EventName, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrap_CarriesTheAggregateMetadataOnTheEnvelope()
    {
        var envelope = Wrap(new RecipeRenamed(new RecipeId(Guid.NewGuid()), "Pizza", 5m, DateTimeOffset.UnixEpoch));

        Assert.Equal(EventId, envelope.EventId);
        Assert.Equal(OccurredAt, envelope.OccurredAt);
        Assert.Equal("recipe", envelope.AggregateName);
        Assert.Equal("recipe-1", envelope.AggregateId);
        Assert.Equal(7, envelope.Version);
    }

    [Fact]
    public void Wrap_WithAnUnregisteredEvent_ThrowsAndNamesTheRegistrationCall()
    {
        var serializer = new DomainEventEnvelopeSerializer(new DomainEventTypeRegistry([]));

        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.Wrap(
                new RecipeRenamed(new RecipeId(Guid.NewGuid()), "Pizza", 5m, DateTimeOffset.UnixEpoch),
                EventId,
                "recipe",
                "recipe-1",
                1,
                OccurredAt));

        Assert.Contains("AddDomainEventsFrom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unwrap_WithAnUnknownEventName_ThrowsAClearException_NotNullReference()
    {
        var envelope = new DomainEventEnvelope("does-not-exist", "{}", EventId, "recipe", "recipe-1", 1, OccurredAt);

        var exception = Record.Exception(() => Serializer.Unwrap(envelope));

        Assert.NotNull(exception);
        Assert.IsNotType<NullReferenceException>(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Wrap_WritesTypedKeysAsBareValues_NotAsObjectsWithComputedMembers()
    {
        var envelope = Wrap(new RecipeRenamed(
            new RecipeId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            "Pizza",
            5m,
            DateTimeOffset.UnixEpoch));

        Assert.Contains("""
            "RecipeId":"11111111-1111-1111-1111-111111111111"
            """, envelope.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEmpty", envelope.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void StoredPayload_SurvivesARenameOfTheClrType()
    {
        var registry = new DomainEventTypeRegistry([typeof(DomainEventEnvelopeSerializerTests).Assembly]);
        var envelope = new DomainEventEnvelope(
            "recipe-created-v1",
            """{"RecipeId":"11111111-1111-1111-1111-111111111111"}""",
            EventId,
            "recipe",
            "recipe-1",
            1,
            OccurredAt);

        var restored = new DomainEventEnvelopeSerializer(registry).Unwrap(envelope);

        Assert.IsType<RecipeCreatedAfterRename>(restored);
    }

    private static DomainEventEnvelope Wrap(IDomainEvent domainEvent) =>
        Serializer.Wrap(domainEvent, EventId, "recipe", "recipe-1", 7, OccurredAt);

    [EventName("recipe-renamed-v1")]
    private sealed record RecipeRenamed(RecipeId RecipeId, string NewName, decimal Rating, DateTimeOffset RenamedAt)
        : DomainEvent;

    [EventName("recipe-created-v1")]
    private sealed record RecipeCreatedAfterRename(RecipeId RecipeId) : DomainEvent;

    private readonly record struct RecipeId(Guid Value) : IEntityKey<Guid>
    {
        public bool IsEmpty => Value == Guid.Empty;
    }
}
