using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStoredWithMessaging;

public sealed record DeleteReading(int Id) : ICommand<ReadingOperationResponse>;
