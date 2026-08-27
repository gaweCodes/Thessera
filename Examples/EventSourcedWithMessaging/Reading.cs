using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;

namespace EventSourcedWithMessaging;

[AggregateName("reading")]
public sealed class Reading : EventSourcedAggregateRoot<ReadingId, ReadingState>
{
    private Reading() : base(ReadingState.Empty)
    {
    }

    public int Value => State.Value;
    public DateTimeOffset CreatedAt => State.CreatedAt;
    public DateTimeOffset? UpdatedAt => State.UpdatedAt;
    public bool IsDeleted => State.IsDeleted;
    public DateTimeOffset? DeletedAt => State.DeletedAt;
    public long Version => State.Version;

    public static Reading Record(ReadingId id, int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));

        var reading = new Reading();
        reading.RaiseEvent(new ReadingCreated(id, value, DateTimeOffset.UtcNow));
        return reading;
    }

    public void ChangeValue(int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));
        RaiseEvent(new ReadingUpdated(Id, value, DateTimeOffset.UtcNow));
    }

    public void Delete() => RaiseEvent(new ReadingDeleted(Id, DateTimeOffset.UtcNow));
}
