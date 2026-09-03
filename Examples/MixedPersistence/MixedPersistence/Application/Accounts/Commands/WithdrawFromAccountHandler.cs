using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistence;

public sealed class WithdrawFromAccountHandler(IRepository<Account, AccountId> repository)
    : ICommandHandler<WithdrawFromAccount, AccountOperationResponse>
{
    public async Task<Result<AccountOperationResponse>> HandleAsync(WithdrawFromAccount command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(new AccountId(command.Id), cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Failure.NotFound("account.not_found", "Account not found.");
        }

        if (account.IsClosed)
        {
            return Failure.Conflict("account.closed", "The account is closed.");
        }

        try
        {
            account.Withdraw(command.Amount);
            return AccountResponseFactory.ForMutation("Withdraw", account);
        }
        catch (DomainValidationException exception)
        {
            return Failure.Validation(exception.Violations[0].Code, exception.Message);
        }
        catch (BusinessRuleViolationException exception)
        {
            return Failure.BusinessRule(exception.Violations[0].Code, exception.Message);
        }
    }
}
