using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourcedWithMessaging;

public sealed record ListReadings() : IQuery<ReadingListResponse>;
