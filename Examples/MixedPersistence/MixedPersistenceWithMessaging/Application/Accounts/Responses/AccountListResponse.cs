namespace MixedPersistenceWithMessaging;

public sealed record AccountListResponse(
    string Operation,
    IReadOnlyList<AccountSnapshot> Accounts);
