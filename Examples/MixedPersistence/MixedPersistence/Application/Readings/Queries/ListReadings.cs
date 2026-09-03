using GaWeCodes.Thessera.Application.Cqrs;

namespace MixedPersistence;

public sealed record ListReadings() : IQuery<ReadingListResponse>;
