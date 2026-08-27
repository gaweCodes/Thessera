using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStoredWithMessaging;

public sealed record CreateReading(int Value) : ICommand<ReadingOperationResponse>;
