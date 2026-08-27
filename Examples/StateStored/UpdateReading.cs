using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStored;

public sealed record UpdateReading(int Id, int Value) : ICommand<ReadingOperationResponse>;
