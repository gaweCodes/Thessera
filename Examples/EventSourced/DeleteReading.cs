using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourced;

public sealed record DeleteReading(int Id) : ICommand<ReadingOperationResponse>;
