using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStored;

public sealed record DeleteReading(int Id) : ICommand<ReadingOperationResponse>;
