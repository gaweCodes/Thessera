using GaWeCodes.Thessera.Application.Results;

namespace MixedPersistenceWithMessaging;

public sealed record ResultEnvelope(bool Success, string Operation, object? Value, IReadOnlyList<FailureView> Failures)
{
    public static ResultEnvelope From<TResult>(Result<TResult> result)
        where TResult : notnull
    {
        if (result.IsSuccess)
        {
            var operation = result.Value switch
            {
                ReadingOperationResponse readingMutation => readingMutation.Operation,
                ReadingListResponse readingList => readingList.Operation,
                AccountOperationResponse accountMutation => accountMutation.Operation,
                AccountListResponse accountList => accountList.Operation,
                _ => "Result",
            };

            return new ResultEnvelope(true, operation, result.Value, []);
        }

        return new ResultEnvelope(false, "Failure", null, [.. result.Failures.Select(FailureView.From)]);
    }

    public static ResultEnvelope FromFailure(string operation, string code, string message) =>
        new(false, operation, null, [new FailureView(code, message, FailureCategory.Validation.ToString(), null)]);
}
