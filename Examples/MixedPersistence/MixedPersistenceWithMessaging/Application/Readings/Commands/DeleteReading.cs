using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record DeleteReading(int Id) : ICommand<ReadingOperationResponse>;
