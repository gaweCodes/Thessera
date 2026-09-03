using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record WithdrawFromAccount(int Id, decimal Amount) : ICommand<AccountOperationResponse>;
