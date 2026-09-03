using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistenceWithMessaging;

public sealed class CloseAccountHandler(IRepository<Account, AccountId> repository)
    : ICommandHandler<CloseAccount, AccountOperationResponse>
{
    public async Task<Result<AccountOperationResponse>> HandleAsync(CloseAccount command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(new AccountId(command.Id), cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Failure.NotFound("account.not_found", "Account not found.");
        }

        if (account.IsClosed)
        {
            return Failure.Conflict("account.closed", "The account is already closed.");
        }

        try
        {
            account.Close();
            return AccountResponseFactory.ForMutation("Close", account);
        }
        catch (BusinessRuleViolationException exception)
        {
            return Failure.BusinessRule(exception.Violations[0].Code, exception.Message);
        }
    }
}
