using System.Collections.Concurrent;

namespace MixedPersistenceWithMessaging;

/// <summary>
/// An in-memory stand-in for a dedicated read database. It is deliberately the only thing
/// <see cref="AccountReadModelRebuilder"/> writes to and <see cref="ListAccountsHandler"/> reads
/// from - the write table underneath is never touched to answer a query.
/// </summary>
public sealed class AccountReadModelStore : IAccountReadModelStore
{
    private readonly ConcurrentDictionary<int, AccountSnapshot> _rows = new();

    public void Clear() => _rows.Clear();

    public void Upsert(AccountSnapshot snapshot) => _rows[snapshot.Id] = snapshot;

    public IReadOnlyCollection<AccountSnapshot> All() => [.. _rows.Values];
}
