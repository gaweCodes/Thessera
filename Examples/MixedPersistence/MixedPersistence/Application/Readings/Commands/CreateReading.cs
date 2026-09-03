using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record CreateReading(int Value) : ICommand<ReadingOperationResponse>;
