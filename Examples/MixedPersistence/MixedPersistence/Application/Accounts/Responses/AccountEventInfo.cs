using GaWeCodes.Thessera.Domain.Events;

namespace MixedPersistence;

public sealed record AccountEventInfo(string Type, int AccountId, decimal? Amount, DateTimeOffset OccurredAt)
{
    public static AccountEventInfo From(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountOpened opened => new(nameof(AccountOpened), opened.AccountId.Value, opened.InitialBalance, opened.OccurredAt),
        AccountDeposited deposited => new(nameof(AccountDeposited), deposited.AccountId.Value, deposited.Amount, deposited.OccurredAt),
        AccountWithdrawn withdrawn => new(nameof(AccountWithdrawn), withdrawn.AccountId.Value, withdrawn.Amount, withdrawn.OccurredAt),
        AccountClosed closed => new(nameof(AccountClosed), closed.AccountId.Value, null, closed.OccurredAt),
        _ => throw new InvalidOperationException($"Unknown domain event '{domainEvent.GetType().Name}'."),
    };
}
