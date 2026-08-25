using GaWeCodes.Thessera.Domain.Rules;

namespace GaWeCodes.Thessera.Tests.TestDoubles;

internal sealed class FakeBusinessRule(
    bool isBroken,
    string message = "business rule broken",
    string code = "test.broken")
    : IBusinessRule
{
    public bool Evaluated { get; private set; }

    public string Code => code;

    public string Message => message;

    public bool IsBroken()
    {
        Evaluated = true;
        return isBroken;
    }
}

internal sealed class FakeValidationRule(
    bool isInvalid,
    string message = "validation rule invalid",
    string code = "test.invalid",
    string? target = null)
    : IDomainValidationRule
{
    public bool Evaluated { get; private set; }

    public string Code => code;

    public string? Target => target;

    public string Message => message;

    public bool IsInvalid()
    {
        Evaluated = true;
        return isInvalid;
    }
}
