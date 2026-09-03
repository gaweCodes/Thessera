using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record OpenAccount(decimal InitialBalance) : ICommand<AccountOperationResponse>;
