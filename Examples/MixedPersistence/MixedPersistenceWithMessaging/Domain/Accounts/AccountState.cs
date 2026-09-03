using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Events;

namespace MixedPersistenceWithMessaging;

public sealed record AccountState(
    AccountId Id,
    decimal Balance,
    DateTimeOffset OpenedAt,
    bool IsClosed,
    DateTimeOffset? ClosedAt) : AggregateState<AccountState, AccountId>
{
    public static AccountState Empty => new(default, 0m, DateTimeOffset.MinValue, false, null);

    public override AccountState Apply(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountOpened opened => this with
        {
            Id = opened.AccountId,
            Balance = opened.InitialBalance,
            OpenedAt = opened.OccurredAt,
            IsClosed = false,
            ClosedAt = null,
        },
        AccountDeposited deposited => this with
        {
            Balance = Balance + deposited.Amount,
        },
        AccountWithdrawn withdrawn => this with
        {
            Balance = Balance - withdrawn.Amount,
        },
        AccountClosed closed => this with
        {
            IsClosed = true,
            ClosedAt = closed.OccurredAt,
        },
        _ => this,
    };
}
