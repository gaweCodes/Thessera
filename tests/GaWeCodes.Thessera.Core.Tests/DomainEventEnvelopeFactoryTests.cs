using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Core.Persistence;
using GaWeCodes.Thessera.Domain;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Tests;

public sealed class DomainEventEnvelopeFactoryTests
{
    private static readonly DateTimeOffset CommitTime = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheVersionsCountUpToTheAggregatesCurrentVersion()
    {
        var counter = Counter.Create(new CounterId(Guid.NewGuid()));
        counter.Increment(1);
        counter.Increment(2);

        var envelopes = Wrap(new StubEntry(counter, "counter", "c-1", 7));

        Assert.Equal([5L, 6L, 7L], envelopes.Select(envelope => envelope.Version));
    }

    [Fact]
    public void EveryEnvelopeOfACommitCarriesTheSameOccurredAt()
    {
        var counter = Counter.Create(new CounterId(Guid.NewGuid()));
        counter.Increment(1);

        var other = Counter.Create(new CounterId(Guid.NewGuid()));

        var envelopes = Wrap(
            new StubEntry(counter, "counter", "c-1", 2),
            new StubEntry(other, "counter", "c-2", 1));

        Assert.All(envelopes, envelope => Assert.Equal(CommitTime, envelope.OccurredAt));
    }

    [Fact]
    public void EveryEnvelopeGetsItsOwnEventId()
    {
        var counter = Counter.Create(new CounterId(Guid.NewGuid()));
        counter.Increment(1);

        var envelopes = Wrap(new StubEntry(counter, "counter", "c-1", 2));

        Assert.Equal(2, envelopes.Select(envelope => envelope.EventId).Distinct().Count());
    }

    [Fact]
    public void AnAggregateWithoutUncommittedEvents_ContributesNothing()
    {
        var counter = Counter.Create(new CounterId(Guid.NewGuid()));
        ((IDomainEventOwner)counter).ClearDomainEvents();

        Assert.Empty(Wrap(new StubEntry(counter, "counter", "c-1", 1)));
    }

    private static IReadOnlyList<DomainEventEnvelope> Wrap(params ITrackedAggregate[] entries)
    {
        var factory = new DomainEventEnvelopeFactory(
            new DomainEventEnvelopeSerializer(new DomainEventTypeRegistry([typeof(CounterCreated).Assembly])),
            new StoppedClock(CommitTime));

        return factory.WrapUncommitted(entries);
    }

    private sealed record StubEntry(
        IDomainEventOwner Aggregate,
        string AggregateName,
        string AggregateId,
        long CurrentVersion) : ITrackedAggregate;

    private sealed class StoppedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }
}
