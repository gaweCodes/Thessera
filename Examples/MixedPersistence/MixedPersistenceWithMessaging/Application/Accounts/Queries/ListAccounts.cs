using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record ListAccounts() : IQuery<AccountListResponse>;
