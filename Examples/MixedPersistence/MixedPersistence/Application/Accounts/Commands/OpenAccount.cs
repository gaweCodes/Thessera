using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record OpenAccount(decimal InitialBalance) : ICommand<AccountOperationResponse>;
