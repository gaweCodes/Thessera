using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record CloseAccount(int Id) : ICommand<AccountOperationResponse>;
