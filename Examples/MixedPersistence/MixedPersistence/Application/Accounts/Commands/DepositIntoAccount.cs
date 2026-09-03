using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record DepositIntoAccount(int Id, decimal Amount) : ICommand<AccountOperationResponse>;
