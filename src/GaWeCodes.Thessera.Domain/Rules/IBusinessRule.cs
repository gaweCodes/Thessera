namespace GaWeCodes.Thessera.Domain.Rules;

public interface IBusinessRule
{
    string Code { get; }

    string Message { get; }

    bool IsBroken();
}
