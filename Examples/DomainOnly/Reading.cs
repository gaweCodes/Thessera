using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;

namespace DomainOnly;

[AggregateName("reading")]
public sealed class Reading : AggregateRoot<ReadingId, ReadingState>
{
    private Reading() : base(ReadingState.Empty)
    {
    }

    public int Value => State.Value;

    public bool IsRemoved => State.IsRemoved;

    public static Reading Record(ReadingId id, int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));

        var reading = new Reading();
        reading.RaiseEvent(new ReadingRecorded(id, value));
        return reading;
    }

    public void ChangeValue(int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));

        RaiseEvent(new ReadingValueChanged(Id, value));
    }

    public void Remove() => RaiseEvent(new ReadingRemoved(Id));

    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var events = DomainEvents.ToArray();
        ((IDomainEventOwner)this).ClearDomainEvents();
        return events;
    }
}
