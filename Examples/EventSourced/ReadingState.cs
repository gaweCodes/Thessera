using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;

namespace EventSourced;

public sealed record ReadingState(
    ReadingId Id,
    int Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt) : AggregateState<ReadingState, ReadingId>
{
    public static ReadingState Empty => new(default, 0, DateTimeOffset.MinValue, null, false, null);

    public override ReadingState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        ReadingCreated created => this with
        {
            Id = created.ReadingId,
            Value = created.Value,
            CreatedAt = created.OccurredAt,
            UpdatedAt = null,
            IsDeleted = false,
            DeletedAt = null,
        },
        ReadingUpdated updated => this with
        {
            Value = updated.Value,
            UpdatedAt = updated.OccurredAt,
        },
        ReadingDeleted deleted => this with
        {
            IsDeleted = true,
            DeletedAt = deleted.OccurredAt,
        },
        _ => this,
    };
}
