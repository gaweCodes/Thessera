using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Entities;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;

namespace DomainOnlyHost;

public readonly record struct ReadingId(Guid Value) : IEntityKey<Guid>
{
    public bool IsEmpty => Value == Guid.Empty;

    public static ReadingId New() => new(Guid.NewGuid());
}

[EventName("matrix-reading-recorded-v1")]
public sealed record ReadingRecorded(ReadingId ReadingId, int Value) : DomainEvent;

[EventName("matrix-reading-corrected-v1")]
public sealed record ReadingCorrected(ReadingId ReadingId, int Value) : DomainEvent;

public sealed record ReadingValueMustBePositive(int Value) : IDomainValidationRule
{
    public string Code => "reading.value.not-positive";

    public string? Target => nameof(Value);

    public string Message => "A reading must carry a positive value.";

    public bool IsInvalid() => Value <= 0;
}

public sealed record ReadingState(ReadingId Id, int Value) : AggregateState<ReadingState, ReadingId>
{
    public static ReadingState Empty => new(default, 0);

    public override ReadingState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ReadingRecorded recorded => this with { Id = recorded.ReadingId, Value = recorded.Value },
        ReadingCorrected corrected => this with { Value = corrected.Value },
        _ => this,
    };
}

[AggregateName("matrix-reading")]
public sealed class Reading : AggregateRoot<ReadingId, ReadingState>
{
    private Reading() : base(ReadingState.Empty)
    {
    }

    public int Value => State.Value;

    public static Reading Record(ReadingId id, int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));

        var reading = new Reading();
        reading.RaiseEvent(new ReadingRecorded(id, value));
        return reading;
    }

    public void Correct(int value)
    {
        RuleChecker.CheckValidationRule(new ReadingValueMustBePositive(value));

        RaiseEvent(new ReadingCorrected(Id, value));
    }
}
