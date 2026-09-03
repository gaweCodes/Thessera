using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistenceWithMessaging;

public sealed record AccountOpeningBalanceMustNotBeNegative(decimal InitialBalance) : IDomainValidationRule
{
    public string Code => "account.opening-balance.negative";

    public string? Target => nameof(InitialBalance);

    public string Message => "An account cannot be opened with a negative balance.";

    public bool IsInvalid() => InitialBalance < 0m;
}
