using GaWeCodes.Thessera.Application.Results;

namespace DomainApplication;

public sealed record FailureView(string Code, string Message, string Category, string? Target)
{
    public static FailureView From(Failure failure) =>
        new(failure.Code, failure.Message, failure.Category.ToString(), failure.Target);
}
