using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistence;

/// <summary>
/// A business rule, not a validation rule: whether it is broken depends on the account's current
/// balance, not on the withdrawal amount alone.
/// </summary>
public sealed record AccountMustHaveSufficientFunds(decimal Balance, decimal Amount) : IBusinessRule
{
    public string Code => "account.insufficient-funds";

    public string Message => "The account does not hold enough balance for this withdrawal.";

    public bool IsBroken() => Amount > Balance;
}
