using GaWeCodes.Thessera.Application.Results;

namespace MixedPersistence;

public static class AccountResponseFactory
{
    public static Result<AccountOperationResponse> ForMutation(string operation, Account account) =>
        new AccountOperationResponse(
            operation,
            AccountSnapshot.From(account),
            [.. account.DomainEvents.Select(AccountEventInfo.From)]);
}
