namespace MixedPersistenceWithMessaging;

public sealed record AccountSnapshot(
    int Id,
    decimal Balance,
    DateTimeOffset OpenedAt,
    bool IsClosed,
    DateTimeOffset? ClosedAt,
    long Version)
{
    public static AccountSnapshot From(Account account) =>
        new(account.Id.Value, account.Balance, account.OpenedAt, account.IsClosed, account.ClosedAt, account.Version);

    public static AccountSnapshot From(AccountState state) =>
        new(state.Id.Value, state.Balance, state.OpenedAt, state.IsClosed, state.ClosedAt, state.Version);
}
