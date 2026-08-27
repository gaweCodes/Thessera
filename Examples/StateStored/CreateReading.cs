using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStored;

public sealed record CreateReading(int Value) : ICommand<ReadingOperationResponse>;
