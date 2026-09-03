using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record CloseAccount(int Id) : ICommand<AccountOperationResponse>;
