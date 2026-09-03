using GaWeCodes.Thessera.Domain.Aggregates;
using GaWeCodes.Thessera.Domain.Naming;
using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistence;

/// <summary>
/// The state-stored half of this example: only the current balance matters, overwritten on every
/// change, through <c>GaWeCodes.Thessera.Persistence.EfCore.Postgres</c>. It is the aggregate this
/// host's EF Core store owns as its <em>main</em> store - the one selected without
/// <c>forAggregates</c> - while <see cref="Reading"/>, in the very same host, is claimed explicitly
/// by the Marten ancillary store. See <see cref="MixedPersistenceApplication"/>.
/// </summary>
[AggregateName("account")]
public sealed class Account : AggregateRoot<AccountId, AccountState>
{
    private Account() : base(AccountState.Empty)
    {
    }

    public decimal Balance => State.Balance;

    public DateTimeOffset OpenedAt => State.OpenedAt;

    public bool IsClosed => State.IsClosed;

    public DateTimeOffset? ClosedAt => State.ClosedAt;

    public long Version => State.Version;

    public static Account Open(AccountId id, decimal initialBalance)
    {
        RuleChecker.CheckValidationRule(new AccountOpeningBalanceMustNotBeNegative(initialBalance));

        var account = new Account();
        account.RaiseEvent(new AccountOpened(id, initialBalance, DateTimeOffset.UtcNow));
        return account;
    }

    public void Deposit(decimal amount)
    {
        RuleChecker.CheckValidationRule(new AccountAmountMustBePositive(amount));
        RaiseEvent(new AccountDeposited(Id, amount, DateTimeOffset.UtcNow));
    }

    public void Withdraw(decimal amount)
    {
        RuleChecker.CheckValidationRule(new AccountAmountMustBePositive(amount));
        RuleChecker.CheckBusinessRule(new AccountMustHaveSufficientFunds(Balance, amount));
        RaiseEvent(new AccountWithdrawn(Id, amount, DateTimeOffset.UtcNow));
    }

    public void Close()
    {
        RuleChecker.CheckBusinessRule(new AccountMustNotHoldABalanceToClose(Balance));
        RaiseEvent(new AccountClosed(Id, DateTimeOffset.UtcNow));
    }
}
