using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;

namespace DomainOnly;

public sealed record ReadingState(ReadingId Id, int Value, bool IsRemoved) : AggregateState<ReadingState, ReadingId>
{
    public static ReadingState Empty => new(default, 0, false);

    public override ReadingState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ReadingRecorded recorded => this with { Id = recorded.ReadingId, Value = recorded.Value },
        ReadingValueChanged changed => this with { Value = changed.Value },
        ReadingRemoved => this with { IsRemoved = true },
        _ => this,
    };
}
