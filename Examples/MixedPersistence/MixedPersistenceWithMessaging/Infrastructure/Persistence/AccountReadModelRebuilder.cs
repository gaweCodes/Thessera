using GaWeCodes.Thessera.Application.ReadModels;

namespace MixedPersistenceWithMessaging;

/// <summary>
/// Projects the current state of an <see cref="Account"/> into <see cref="IAccountReadModelStore"/>.
/// Discovered and registered automatically because it implements
/// <see cref="IReadModelRebuilder{TAggregate, TKey}"/>; <c>StateStoredReadModelRebuildRunner{TContext}</c>
/// is what drives it.
/// </summary>
public sealed class AccountReadModelRebuilder(IAccountReadModelStore store) : IReadModelRebuilder<Account, AccountId>
{
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        store.Clear();
        return Task.CompletedTask;
    }

    public Task RebuildAsync(Account aggregate, CancellationToken cancellationToken)
    {
        store.Upsert(AccountSnapshot.From(aggregate));
        return Task.CompletedTask;
    }
}
