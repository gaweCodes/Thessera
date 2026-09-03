using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record CreateReading(int Value) : ICommand<ReadingOperationResponse>;
