using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Domain.Rules;

namespace MixedPersistenceWithMessaging;

public sealed class OpenAccountHandler(IRepository<Account, AccountId> repository, IAccountIdSequence idSequence)
    : ICommandHandler<OpenAccount, AccountOperationResponse>
{
    public async Task<Result<AccountOperationResponse>> HandleAsync(OpenAccount command, CancellationToken cancellationToken)
    {
        var accountId = idSequence.ReserveNext();

        try
        {
            var account = Account.Open(accountId, command.InitialBalance);
            await repository.AddAsync(account, cancellationToken).ConfigureAwait(false);
            return AccountResponseFactory.ForMutation("Open", account);
        }
        catch (DomainValidationException exception)
        {
            idSequence.TryRelease(accountId);
            return Failure.Validation(exception.Violations[0].Code, exception.Message);
        }
    }
}
