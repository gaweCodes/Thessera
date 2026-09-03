using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record WithdrawFromAccount(int Id, decimal Amount) : ICommand<AccountOperationResponse>;
