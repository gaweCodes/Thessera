using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistenceWithMessaging;

public sealed record UpdateReading(int Id, int Value) : ICommand<ReadingOperationResponse>;
