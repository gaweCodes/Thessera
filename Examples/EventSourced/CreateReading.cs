using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourced;

public sealed record CreateReading(int Value) : ICommand<ReadingOperationResponse>;
