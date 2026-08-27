using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStoredWithMessaging;

public sealed record UpdateReading(int Id, int Value) : ICommand<ReadingOperationResponse>;
