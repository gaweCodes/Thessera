using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;

namespace MixedPersistence;

public sealed class ListAccountsHandler(IAccountReadModelStore readModel) : IQueryHandler<ListAccounts, AccountListResponse>
{
    public Task<Result<AccountListResponse>> HandleAsync(ListAccounts query, CancellationToken cancellationToken)
    {
        var accounts = readModel.All()
            .OrderBy(snapshot => snapshot.OpenedAt)
            .ToList();

        Result<AccountListResponse> result = new AccountListResponse("List", accounts);
        return Task.FromResult(result);
    }
}
