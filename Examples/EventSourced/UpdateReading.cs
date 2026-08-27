using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourced;

public sealed record UpdateReading(int Id, int Value) : ICommand<ReadingOperationResponse>;
