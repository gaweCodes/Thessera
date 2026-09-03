using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record ListAccounts() : IQuery<AccountListResponse>;
