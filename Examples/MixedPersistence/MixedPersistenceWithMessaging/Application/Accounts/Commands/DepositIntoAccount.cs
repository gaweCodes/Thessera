using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record DepositIntoAccount(int Id, decimal Amount) : ICommand<AccountOperationResponse>;
