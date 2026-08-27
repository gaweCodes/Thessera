using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourcedWithMessaging;

public sealed record UpdateReading(int Id, int Value) : ICommand<ReadingOperationResponse>;
