using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record UpdateReading(int Id, int Value) : ICommand<ReadingOperationResponse>;
