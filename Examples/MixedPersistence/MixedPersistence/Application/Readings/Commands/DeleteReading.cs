using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record DeleteReading(int Id) : ICommand<ReadingOperationResponse>;
