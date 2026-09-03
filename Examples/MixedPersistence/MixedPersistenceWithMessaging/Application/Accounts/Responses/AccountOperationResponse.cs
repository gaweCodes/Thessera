namespace MixedPersistenceWithMessaging;

public sealed record AccountOperationResponse(
    string Operation,
    AccountSnapshot Account,
    IReadOnlyList<AccountEventInfo> DomainEvents);
