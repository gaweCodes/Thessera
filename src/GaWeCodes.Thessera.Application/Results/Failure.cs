namespace GaWeCodes.Thessera.Application.Results;

public sealed record Failure
{
    public Failure(string code, string message, FailureCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "The failure category must be one of the declared values, because the transport layer maps it to a status code.");
        }

        Code = code;
        Message = message;
        Category = category;
    }

    public string Code { get; }

    public string Message { get; }

    public FailureCategory Category { get; }

    public string? Target { get; init; }

    public static Failure Validation(string code, string message) => new(code, message, FailureCategory.Validation);

    public static Failure BusinessRule(string code, string message) => new(code, message, FailureCategory.BusinessRule);

    public static Failure NotFound(string code, string message) => new(code, message, FailureCategory.NotFound);

    public static Failure Conflict(string code, string message) => new(code, message, FailureCategory.Conflict);

    public static Failure Forbidden(string code, string message) => new(code, message, FailureCategory.Forbidden);
}
