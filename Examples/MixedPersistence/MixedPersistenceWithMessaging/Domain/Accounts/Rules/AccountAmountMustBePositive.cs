using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistenceWithMessaging;

public sealed record AccountAmountMustBePositive(decimal Amount) : IDomainValidationRule
{
    public string Code => "account.amount.not-positive";

    public string? Target => nameof(Amount);

    public string Message => "A deposit or withdrawal amount must be positive.";

    public bool IsInvalid() => Amount <= 0m;
}
