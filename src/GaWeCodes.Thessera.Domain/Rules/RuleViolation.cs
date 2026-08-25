namespace GaWeCodes.Thessera.Domain.Rules;

public sealed record RuleViolation
{
    public RuleViolation(string code, string? target, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Target = target;
        Message = message;
    }

    public string Code { get; }

    public string? Target { get; }

    public string Message { get; }
}
