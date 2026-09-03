namespace MixedPersistenceWithMessaging;

/// <summary>
/// A dedicated read side for <see cref="Account"/>, kept separate from <see cref="AccountDbContext"/>
/// so <see cref="ListAccountsHandler"/> never has to query the write table to answer a query.
/// </summary>
public interface IAccountReadModelStore
{
    void Clear();

    void Upsert(AccountSnapshot snapshot);

    IReadOnlyCollection<AccountSnapshot> All();
}
