using GaWeCodes.Thessera.Application.Cqrs;

namespace StateStored;

public sealed record ListReadings() : IQuery<ReadingListResponse>;
