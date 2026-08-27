using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourcedWithMessaging;

public sealed record CreateReading(int Value) : ICommand<ReadingOperationResponse>;
