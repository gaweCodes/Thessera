namespace MixedPersistence;

public sealed record AccountListResponse(
    string Operation,
    IReadOnlyList<AccountSnapshot> Accounts);
