using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistence;

public sealed record AccountMustNotHoldABalanceToClose(decimal Balance) : IBusinessRule
{
    public string Code => "account.close.balance-not-zero";

    public string Message => "An account may not be closed while it holds a balance.";

    public bool IsBroken() => Balance != 0m;
}
