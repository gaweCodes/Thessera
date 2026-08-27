using GaWeCodes.Thessera.Domain.Rules;

namespace StateStored;

public sealed record ReadingValueMustBePositive(int Value) : IDomainValidationRule
{
    public string Code => "reading.value.not-positive";

    public string? Target => nameof(Value);

    public string Message => "A reading must carry a positive value.";

    public bool IsInvalid() => Value <= 0;
}
