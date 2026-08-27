using GaWeCodes.Thessera.Application.Cqrs;

namespace EventSourced;

public sealed record ListReadings() : IQuery<ReadingListResponse>;
