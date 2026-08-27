using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourcedWithMessaging;

public sealed record DeleteReading(int Id) : ICommand<ReadingOperationResponse>;
